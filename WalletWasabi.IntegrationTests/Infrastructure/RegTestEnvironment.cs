using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
using WalletWasabi.Tests.BitcoinCore;
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
			CpfpInfoProvider);

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
	/// Synchronizes filters from Bitcoin Core RPC.
	/// This fetches all compact block filters from the current tip and stores them.
	/// </summary>
	public async Task SyncFiltersAsync(CancellationToken cancellationToken = default)
	{
		var blockHeaderChain = new ConcurrentChain(Network);
		var filterProvider = FilterProviders.CreateBitcoinRpcFilterProvider(RpcClient, blockHeaderChain);

		var tip = FilterStore.GetTip();
		uint fromHeight = (uint)(tip?.Header.Height ?? ChainHeight.Genesis);
		uint256 fromHash = tip?.Header.BlockHash ?? await RpcClient.GetBlockHashAsync(0, cancellationToken).ConfigureAwait(false);

		while (true)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var result = await filterProvider(fromHeight, fromHash, cancellationToken).ConfigureAwait(false);

			if (result.IsOk && result.Value is FiltersResponse.NewFiltersAvailable newFilters)
			{
				await FilterStore.AddNewFiltersAsync(newFilters.Filters).ConfigureAwait(false);

				var lastFilter = newFilters.Filters.LastOrDefault();
				if (lastFilter is not null)
				{
					fromHeight = (uint)lastFilter.Header.Height;
					fromHash = lastFilter.Header.BlockHash;
				}
			}
			else if (result.IsOk && result.Value is FiltersResponse.AlreadyOnBestBlock)
			{
				break;
			}
			else
			{
				// Error or unknown state - wait a bit and retry
				await Task.Delay(100, cancellationToken).ConfigureAwait(false);
			}
		}
	}

	/// <summary>
	/// Synchronizes filters from Bitcoin Core P2P network.
	/// This uses the compact filter protocol (BIP 157/158) to fetch filters from a connected peer.
	/// </summary>
	public async Task SyncFiltersP2pAsync(CancellationToken cancellationToken = default)
	{
		var blockHeaderChain = new ConcurrentChain(Network);

		// Create the filter synchronization state
		var tip = FilterStore.GetTip();
		var tipHeight = tip?.Header.Height ?? ChainHeight.Genesis;
		var synchronizationState = new CompactFilterBehavior.FilterSynchronizationState(
			blockHeaderChain,
			FilterHeaderChain,
			tipHeight);

		// Wait for Bitcoin Core to finish building the filter index and advertise NODE_COMPACT_FILTERS
		// Bitcoin Core only advertises this flag after the block filter index is fully built
		var maxRetries = 30; // Wait up to 30 seconds
		for (var i = 0; i < maxRetries; i++)
		{
			using var checkNode = await BitcoinCoreNode.CreateNewP2pNodeAsync().ConfigureAwait(false);
			checkNode.VersionHandshake(cancellationToken);

			if (checkNode.SupportsCompactFilters)
			{
				break;
			}

			if (i == maxRetries - 1)
			{
				throw new InvalidOperationException("Bitcoin Core does not advertise NODE_COMPACT_FILTERS. " +
					"Ensure blockfilterindex=1 and peerblockfilters=1 are set in the config.");
			}

			await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
		}

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

		// Manually sync block headers from RPC - ChainBehavior auto-sync may not work
		// reliably in test environments with a single direct node connection
		await SyncBlockHeadersFromRpcAsync(blockHeaderChain, targetHeight, cancellationToken).ConfigureAwait(false);

		// Create the P2P filter provider
		var filterProvider = FilterProviders.CreateBitcoinP2pFilterProvider(
			FilterHeaderChain,
			blockHeaderChain,
			synchronizationState);

		uint fromHeight = (uint)tipHeight;
		uint256 fromHash = tip?.Header.BlockHash ?? await RpcClient.GetBlockHashAsync(0, cancellationToken).ConfigureAwait(false);

		while (true)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var result = await filterProvider(fromHeight, fromHash, cancellationToken).ConfigureAwait(false);

			if (result.IsOk && result.Value is FiltersResponse.NewFiltersAvailable newFilters)
			{
				await FilterStore.AddNewFiltersAsync(newFilters.Filters).ConfigureAwait(false);

				var lastFilter = newFilters.Filters.LastOrDefault();
				if (lastFilter is not null)
				{
					fromHeight = (uint)lastFilter.Header.Height;
					fromHash = lastFilter.Header.BlockHash;

					// Check if we've caught up
					if (fromHeight >= targetHeight)
					{
						break;
					}
				}
			}
			else if (result.IsOk && result.Value is FiltersResponse.AlreadyOnBestBlock)
			{
				break;
			}
			else
			{
				// Error or retry needed - wait a bit
				await Task.Delay(100, cancellationToken).ConfigureAwait(false);
			}
		}
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

	/// <summary>
	/// Syncs block headers from RPC into the ConcurrentChain.
	/// This is more reliable than relying on ChainBehavior auto-sync in test environments.
	/// </summary>
	private async Task SyncBlockHeadersFromRpcAsync(ConcurrentChain chain, int targetHeight, CancellationToken cancellationToken)
	{
		var currentHeight = chain.Height;
		if (currentHeight >= targetHeight)
		{
			return;
		}

		// Fetch headers in batches from current tip to target
		const int batchSize = 2000;
		while (chain.Height < targetHeight)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var startHeight = chain.Height + 1;
			var endHeight = Math.Min(startHeight + batchSize - 1, targetHeight);
			var count = endHeight - startHeight + 1;

			// Get block hashes
			var hashTasks = new Task<uint256>[count];
			var batch = RpcClient.PrepareBatch();
			for (var i = 0; i < count; i++)
			{
				hashTasks[i] = batch.GetBlockHashAsync(startHeight + i, cancellationToken);
			}
			await batch.SendBatchAsync(cancellationToken).ConfigureAwait(false);
			var hashes = await Task.WhenAll(hashTasks).ConfigureAwait(false);

			// Get block headers
			var headerBatch = RpcClient.PrepareBatch();
			var headerTasks = hashes.Select(h => headerBatch.GetBlockHeaderAsync(h, cancellationToken)).ToArray();
			await headerBatch.SendBatchAsync(cancellationToken).ConfigureAwait(false);
			var headers = await Task.WhenAll(headerTasks).ConfigureAwait(false);

			// Add headers to chain
			foreach (var header in headers)
			{
				chain.SetTip(header);
			}
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
