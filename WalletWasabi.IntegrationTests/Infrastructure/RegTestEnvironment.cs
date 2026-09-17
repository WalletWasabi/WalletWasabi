using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using WalletWasabi.Backend.Models;
using WalletWasabi.BitcoinP2p;
using WalletWasabi.Extensions;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.IntegrationTests.BitcoinCore;
using WalletWasabi.Wallets;
using ChainHeight = WalletWasabi.Models.Height.ChainHeight;

namespace WalletWasabi.IntegrationTests.Infrastructure;

/// <summary>
/// Comprehensive test environment for integration tests.
/// Provides access to Bitcoin Core RPC, filter stores, transaction stores, and wallet infrastructure.
/// </summary>
public sealed class RegTestEnvironment : IAsyncDisposable
{
	private RegTestEnvironment(
		IntegrationTestFixture fixture,
		string workDir,
		EventBus eventBus,
		FilterStore filterStore,
		AllTransactionStore transactionStore,
		FilterHeaderChain filterHeaderChain,
		CpfpInfoProvider cpfpInfoProvider)
	{
		Fixture = fixture;
		WorkDir = workDir;
		EventBus = eventBus;
		FilterStore = filterStore;
		TransactionStore = transactionStore;
		FilterHeaderChain = filterHeaderChain;
		CpfpInfoProvider = cpfpInfoProvider;
		ServiceConfiguration = new ServiceConfiguration(Money.Coins(Constants.DefaultDustThreshold));
	}

	public IntegrationTestFixture Fixture { get; }
	public string WorkDir { get; }
	public EventBus EventBus { get; }
	public FilterStore FilterStore { get; }
	public AllTransactionStore TransactionStore { get; }
	public FilterHeaderChain FilterHeaderChain { get; }
	public CpfpInfoProvider CpfpInfoProvider { get; }
	public ServiceConfiguration ServiceConfiguration { get; }

	/// <summary>
	/// Wallet-specific RPC client for operations requiring wallet context (send, generate, etc.).
	/// </summary>
	public IRPCClient RpcClient => Fixture.WalletRpcClient;
	public Network Network => RpcClient.Network;
	public CoreNode BitcoinCoreNode => Fixture.BitcoinCoreNode;
	public MempoolService MempoolService => BitcoinCoreNode.MempoolService;

	public const string DefaultPassword = "password";

	/// <summary>
	/// Creates and initializes a new test environment.
	/// </summary>
	public static async Task<RegTestEnvironment> CreateAsync(
		IntegrationTestFixture fixture,
		[CallerFilePath] string callerFilePath = "",
		[CallerMemberName] string callerMemberName = "")
	{
		string workDir = GetWorkDir(callerFilePath, callerMemberName);

		// Clean up previous test run
		if (Directory.Exists(workDir))
		{
			await IoHelpers.TryDeleteDirectoryAsync(workDir).ConfigureAwait(false);
		}
		Directory.CreateDirectory(workDir);

		var eventBus = new EventBus();
		var filterHeaderChain = new FilterHeaderChain();

		var filterStore = new FilterStore(
			Path.Combine(workDir, "filters"),
			fixture.BitcoinCoreNode.Network,
			filterHeaderChain,
			eventBus);
		await filterStore.InitializeAsync(new ChainHeight(0u), CancellationToken.None).ConfigureAwait(false);

		var transactionStore = new AllTransactionStore(
			Path.Combine(workDir, "transactions"),
			fixture.BitcoinCoreNode.Network);
		await transactionStore.InitializeAsync(CancellationToken.None).ConfigureAwait(false);

		// Use unique worker name to avoid conflicts between tests
		var workerName = $"CpfpInfoProvider_{Guid.NewGuid():N}";
		var cpfpInfoProvider = new CpfpInfoProvider(
			Workers.Spawn(workerName, Workers.EventDriven(Unit.Instance, CpfpInfoUpdater.CreateForRegTest())));

		return new RegTestEnvironment(
			fixture,
			workDir,
			eventBus,
			filterStore,
			transactionStore,
			filterHeaderChain,
			cpfpInfoProvider);
	}

	/// <summary>
	/// Creates a new KeyManager for testing.
	/// </summary>
	public KeyManager CreateKeyManager(string? password = null)
	{
		return KeyManager.CreateNew(out _, password ?? DefaultPassword, Network);
	}

	/// <summary>Hardware wallets are not exercised in these tests, but a wallet needs the service.</summary>
	public HardwareWalletService HardwareWallets { get; } = new(Network.RegTest);

	/// <summary>
	/// Creates a Wallet instance (not started).
	/// </summary>
	public Wallet CreateWallet(KeyManager keyManager)
	{
		var factory = Wallet.CreateFactory(
			Network,
			FilterStore,
			TransactionStore,
			FilterHeaderChain,
			MempoolService,
			ServiceConfiguration,
			CreateBlockProvider(),
			EventBus,
			CpfpInfoProvider,
			HardwareWallets);

		return factory(keyManager);
	}

	/// <summary>
	/// Creates a block provider that fetches blocks via RPC.
	/// </summary>
	public BlockProvider CreateBlockProvider()
	{
		return BlockProviders.RpcBlockProvider(RpcClient);
	}

	/// <summary>
	/// Synchronizes filters from Bitcoin Core RPC using the production Synchronizer.
	/// This fetches all compact block filters from the current tip and stores them.
	/// </summary>
	public async Task SyncFiltersRpcAsync(CancellationToken cancellationToken = default)
	{
		// Build block header chain from RPC - required for reorg detection
		var blockHeaderChain = await BuildBlockHeaderChainAsync(cancellationToken).ConfigureAwait(false);

		var filterProvider = FilterProviders.CreateBitcoinRpcFilterProvider(RpcClient, blockHeaderChain);

		// Use the production Synchronizer's filter generator
		var filterGenerator = Synchronizer.CreateFilterGenerator(filterProvider, FilterStore, FilterHeaderChain, EventBus);

		// Run the synchronizer until we're caught up
		while (true)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var tip = FilterStore.GetTip();
			var currentHeight = await RpcClient.GetBlockCountAsync(cancellationToken).ConfigureAwait(false);

			// Check if we're synced to the current chain tip
			if (tip is not null && (uint)tip.Header.Height >= currentHeight)
			{
				// Verify we're on the right chain (not orphaned)
				var currentHashAtTip = await RpcClient.GetBlockHashAsync((int)(uint)tip.Header.Height, cancellationToken).ConfigureAwait(false);
				if (currentHashAtTip == tip.Header.BlockHash)
				{
					break; // Fully synced and on the right chain
				}
			}

			// Run one iteration of the production synchronizer
			await filterGenerator(Unit.Instance, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Synchronizes filters from Bitcoin Core P2P network using the production Synchronizer.
	/// This uses the compact filter protocol (BIP 157/158) to fetch filters from a connected peer.
	/// </summary>
	public async Task SyncFiltersP2PAsync(CancellationToken cancellationToken = default)
	{
		var blockHeaderChain = new ConcurrentChain(Network);

		// Create the filter synchronization state
		var tip = FilterStore.GetTip();
		var tipHeight = tip?.Header.Height ?? ChainHeight.Genesis;
		var synchronizationState = new FilterSynchronizationState(blockHeaderChain, FilterHeaderChain, tipHeight);

		// Create a P2P connection to Bitcoin Core - behaviors must be added before handshake
		var node = await BitcoinCoreNode.CreateNewP2pNodeAsync().ConfigureAwait(false);

		// Add behaviors for syncing block headers and compact filters
		node.Behaviors.Add(new BlockHeadersChainBehavior(blockHeaderChain, FilterHeaderChain, EventBus));
		node.Behaviors.Add(new CompactFilterBehavior(synchronizationState, blockHeaderChain, EventBus));

		// Start emitting tick events to drive the sync process
		using var tickTimer = new Timer(
			_ => EventBus.Publish(new Tick(DateTime.UtcNow)),
			null,
			TimeSpan.Zero,
			TimeSpan.FromMilliseconds(1000));

		// Connect and handshake
		node.VersionHandshake(cancellationToken);

		// Get the target height from Bitcoin Core
		var targetHeight = await RpcClient.GetBlockCountAsync(cancellationToken).ConfigureAwait(false);

		// Wait for block headers to sync to target height (with timeout)
		// This fixes a race condition where filter sync would start before block headers were ready
		var headerSyncTimeout = TimeSpan.FromSeconds(30);
		var headerSyncDeadline = DateTime.UtcNow + headerSyncTimeout;
		while (blockHeaderChain.Tip?.Height < targetHeight)
		{
			if (DateTime.UtcNow > headerSyncDeadline)
			{
				throw new TimeoutException(
					$"Block headers did not sync to target height {targetHeight} within {headerSyncTimeout.TotalSeconds}s. " +
					$"Current height: {blockHeaderChain.Tip?.Height ?? 0}");
			}
			await Task.Delay(100, cancellationToken).ConfigureAwait(false);
		}

		// Create the P2P filter provider
		var filterProvider = FilterProviders.CreateBitcoinP2pFilterProvider(
			FilterHeaderChain,
			blockHeaderChain,
			synchronizationState);

		// Use the production Synchronizer's filter generator
		var filterGenerator = Synchronizer.CreateFilterGenerator(filterProvider, FilterStore, FilterHeaderChain, EventBus);

		// Run the synchronizer until we're caught up
		while (true)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var currentTip = FilterStore.GetTip();

			// Check if we're synced to the target height
			if (currentTip is not null && (uint)currentTip.Header.Height >= targetHeight)
			{
				break;
			}

			// Run one iteration of the production synchronizer
			await filterGenerator(Unit.Instance, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Builds the block header chain from Bitcoin Core RPC.
	/// This is needed for reorg detection in the filter provider.
	/// </summary>
	private async Task<ConcurrentChain> BuildBlockHeaderChainAsync(CancellationToken cancellationToken)
	{
		// ConcurrentChain(Network) already includes the genesis block
		var chain = new ConcurrentChain(Network);
		var currentHeight = await RpcClient.GetBlockCountAsync(cancellationToken).ConfigureAwait(false);

		// Build chain from height 1 (genesis is already in chain) to current tip
		for (int height = 1; height <= currentHeight; height++)
		{
			var blockHash = await RpcClient.GetBlockHashAsync(height, cancellationToken).ConfigureAwait(false);
			var blockHeader = await RpcClient.GetBlockHeaderAsync(blockHash, cancellationToken).ConfigureAwait(false);
			chain.SetTip(new ChainedBlock(blockHeader, blockHash, chain.Tip));
		}

		return chain;
	}

	/// <summary>
	/// Funds a wallet address and confirms the transaction.
	/// </summary>
	/// <param name="address">The address to fund.</param>
	/// <param name="amount">The amount to send.</param>
	/// <param name="confirmations">Number of confirmations (blocks to generate after sending).</param>
	/// <returns>The transaction ID.</returns>
	public async Task<uint256> FundAddressAsync(BitcoinAddress address, Money amount, int confirmations = 1)
	{
		var txid = await RpcClient.SendToAddressAsync(address, amount).ConfigureAwait(false);

		if (confirmations > 0)
		{
			await RpcClient.GenerateAsync(confirmations).ConfigureAwait(false);
		}

		return txid;
	}

	/// <summary>
	/// Waits for a specific number of events to be published on the EventBus.
	/// </summary>
	/// <typeparam name="TEvent">The type of event to wait for.</typeparam>
	/// <param name="count">The number of events to wait for.</param>
	/// <param name="timeout">Maximum time to wait.</param>
	/// <returns>The collected events.</returns>
	public async Task<List<TEvent>> WaitForEventsAsync<TEvent>(int count, TimeSpan timeout)
		where TEvent : notnull
	{
		var events = new List<TEvent>();
		var completion = new TaskCompletionSource<List<TEvent>>();

		using var cts = new CancellationTokenSource(timeout);
		using var registration = cts.Token.Register(() =>
			completion.TrySetException(new TimeoutException($"Timed out waiting for {count} {typeof(TEvent).Name} events. Received {events.Count}.")));

		using var subscription = EventBus.Subscribe<TEvent>(e =>
		{
			lock (events)
			{
				events.Add(e);
				if (events.Count >= count)
				{
					completion.TrySetResult(new List<TEvent>(events));
				}
			}
		});

		return await completion.Task.ConfigureAwait(false);
	}

	/// <summary>
	/// Waits for a condition to become true.
	/// </summary>
	/// <param name="condition">The condition to check.</param>
	/// <param name="timeout">Maximum time to wait.</param>
	/// <param name="pollInterval">How often to check the condition.</param>
	public async Task WaitForConditionAsync(Func<bool> condition, TimeSpan timeout, TimeSpan? pollInterval = null)
	{
		var interval = pollInterval ?? TimeSpan.FromMilliseconds(100);
		var deadline = DateTime.UtcNow + timeout;

		while (!condition())
		{
			if (DateTime.UtcNow > deadline)
			{
				throw new TimeoutException("Condition was not met within the timeout period.");
			}
			await Task.Delay(interval).ConfigureAwait(false);
		}
	}


	private static string GetWorkDir(string callerFilePath, string callerMemberName)
	{
		var dataDir = EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "IntegrationTests"));
		return Path.Combine(dataDir, EnvironmentHelpers.ExtractFileName(callerFilePath), callerMemberName);
	}

	public ValueTask DisposeAsync()
	{
		FilterStore.Dispose();
		TransactionStore.Dispose();
		return ValueTask.CompletedTask;
	}
}
