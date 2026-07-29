using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using NBitcoin;
using NBitcoin.RPC;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Payjoin;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.Wallets;
using ChainHeight = WalletWasabi.Models.Height.ChainHeight;

namespace WalletWasabi.IntegrationTests.Payjoin;

/// <summary>
/// A real Wasabi wallet stack (KeyManager, Wallet, filter/transaction stores) wired to the
/// payjoin harness's regtest bitcoind, plus factories for the production payjoin components.
/// Block filters are never synced: funding and settlement transactions are fed through the
/// wallet's <see cref="TransactionProcessor"/>, the same path a mempool arrival takes in
/// production, so the round-trip tests exercise the payjoin machinery rather than filter sync
/// (which has its own integration tests).
/// </summary>
internal sealed class WasabiWalletHarness : IAsyncDisposable
{
	/// <summary>All harness traffic is loopback; never let a host proxy intercept it.</summary>
	private sealed class LoopbackHttpClientFactory : IHttpClientFactory, IDisposable
	{
		private readonly SocketsHttpHandler _handler = new() { UseProxy = false };

		public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);

		public void Dispose() => _handler.Dispose();
	}

	private readonly PayjoinHarnessFixture _fixture;
	private readonly FilterStore _filterStore;
	private readonly LoopbackHttpClientFactory _httpClientFactory = new();

	private WasabiWalletHarness(PayjoinHarnessFixture fixture, string workDir, FilterStore filterStore, AllTransactionStore transactionStore, Wallet wallet, IRPCClient rpcClient, TransactionBroadcaster broadcaster)
	{
		_fixture = fixture;
		WorkDir = workDir;
		_filterStore = filterStore;
		TransactionStore = transactionStore;
		Wallet = wallet;
		RpcClient = rpcClient;
		Broadcaster = broadcaster;
	}

	public string WorkDir { get; }
	public AllTransactionStore TransactionStore { get; }
	public Wallet Wallet { get; }
	public KeyManager KeyManager => Wallet.KeyManager;
	public IRPCClient RpcClient { get; }
	public TransactionBroadcaster Broadcaster { get; }
	public IHttpClientFactory HttpClientFactory => _httpClientFactory;

	public static async Task<WasabiWalletHarness> CreateAsync(PayjoinHarnessFixture fixture, string name)
	{
		string workDir = Path.Combine(fixture.RootDir, "wasabi", name);
		Directory.CreateDirectory(workDir);

		// Wasabi's static logger is off until configured; PayjoinManager reports per-session
		// trouble only through it, so route it to the test console for diagnosability.
		WalletWasabi.Logging.Logger.Configure(Path.Combine(workDir, "Logs.txt"), WalletWasabi.Logging.LogLevel.Debug, [WalletWasabi.Logging.LogMode.Console]);

		var eventBus = new EventBus();
		var filterHeaderChain = new FilterHeaderChain();
		var filterStore = new FilterStore(Path.Combine(workDir, "filters"), Network.RegTest, filterHeaderChain, eventBus);
		await filterStore.InitializeAsync(new ChainHeight(0u), CancellationToken.None).ConfigureAwait(false);

		var transactionStore = new AllTransactionStore(Path.Combine(workDir, "transactions"), Network.RegTest);
		await transactionStore.InitializeAsync(CancellationToken.None).ConfigureAwait(false);

		var mempoolService = new MempoolService(eventBus);
		var cpfpInfoProvider = new CpfpInfoProvider(
			Workers.Spawn($"CpfpInfoProvider_{Guid.NewGuid():N}", Workers.EventDriven(Unit.Instance, CpfpInfoUpdater.CreateForRegTest())));

#pragma warning disable CA2000 // Ownership transferred: MemoryCache to CachedRpcClient, broadcaster lives for the harness lifetime.
		var rpcClient = new CachedRpcClient(
			new RPCClient($"{fixture.RpcUser}:{fixture.RpcPassword}", new Uri($"http://127.0.0.1:{fixture.RpcPort}"), Network.RegTest),
			new MemoryCache(new MemoryCacheOptions()));
		var broadcaster = new TransactionBroadcaster([new RpcBroadcaster(rpcClient)], mempoolService);
#pragma warning restore CA2000

		WalletFactory walletFactory = Wallet.CreateFactory(
			Network.RegTest,
			filterStore,
			transactionStore,
			filterHeaderChain,
			mempoolService,
			new ServiceConfiguration(Money.Coins(Constants.DefaultDustThreshold)),
			BlockProviders.RpcBlockProvider(rpcClient),
			eventBus,
			cpfpInfoProvider);

		// Hot wallet with an empty password: the receiver signs its contributed input via
		// KeyManager secrets, exactly like a logged-in desktop wallet.
		KeyManager keyManager = KeyManager.CreateNew(out _, password: "", Network.RegTest);
		Wallet wallet = walletFactory(keyManager);
		if (!wallet.TryLogin("", out _))
		{
			throw new InvalidOperationException("Test wallet login failed.");
		}

		using var startCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		await wallet.StartAsync(startCts.Token).ConfigureAwait(false);

		return new WasabiWalletHarness(fixture, workDir, filterStore, transactionStore, wallet, rpcClient, broadcaster);
	}

	/// <summary>Funds the wallet with one confirmed coin from the bank and registers it with the wallet.</summary>
	public async Task<SmartCoin> FundAsync(Money amount)
	{
		HdPubKey key = KeyManager.GetNextReceiveKey("payjoin-harness-funding");
		BitcoinAddress address = key.GetP2wpkhAddress(Network.RegTest);
		uint256 txid = await _fixture.BankRpc.SendToAddressAsync(address, amount).ConfigureAwait(false);
		await _fixture.MineAsync(1).ConfigureAwait(false);
		await ProcessConfirmedTransactionAsync(txid).ConfigureAwait(false);

		SmartCoin coin = Wallet.Coins.Single(c => c.TransactionId == txid);
		if (!coin.Confirmed)
		{
			throw new InvalidOperationException("Funding coin should be confirmed.");
		}

		return coin;
	}

	/// <summary>
	/// Feeds a just-mined transaction into the wallet at the current tip height. This is the
	/// harness's stand-in for filter-based discovery; only call it after the containing block
	/// was mined as the latest block.
	/// </summary>
	public async Task ProcessConfirmedTransactionAsync(uint256 txid)
	{
		Transaction tx = await _fixture.BankRpc.GetRawTransactionAsync(txid).ConfigureAwait(false);
		int height = await _fixture.BankRpc.GetBlockCountAsync().ConfigureAwait(false);
		Wallet.TransactionProcessor.Process(new SmartTransaction(tx, new ChainHeight((uint)height)));
	}

	/// <summary>
	/// The production receiver service against the harness's plain-HTTP directory and relay.
	/// TorEnabled=true selects the direct well-known OHTTP-keys fetch through the (loopback)
	/// HTTP factory — the relay-as-CONNECT-proxy bootstrap does not work over the plain-HTTP
	/// mailroom; the TLS bootstrap path has its own test.
	/// Each call opens the same session database under <see cref="WorkDir"/>, so a second
	/// manager instance resumes the first one's sessions — that is the crash-recovery story.
	/// </summary>
	public PayjoinManager CreatePayjoinManager()
	{
		var configuration = new PayjoinConfiguration(
			DirectoryUrl: _fixture.Directory.Url,
			OhttpRelayUrls: [_fixture.Relay.Url],
			MaxFeeRateSatPerVb: 1000,
			TorEnabled: true);

		return new PayjoinManager(
			Path.Combine(WorkDir, "payjoin-manager"),
			Network.RegTest,
			configuration,
			() => Task.FromResult<IEnumerable<Wallet>>([Wallet]),
			HttpClientFactory,
			Broadcaster);
	}

	public async Task WaitForConditionAsync(Func<bool> condition, TimeSpan timeout, string description)
	{
		DateTime deadline = DateTime.UtcNow + timeout;
		while (!condition())
		{
			if (DateTime.UtcNow > deadline)
			{
				throw new TimeoutException($"Timed out after {timeout.TotalSeconds:0}s waiting for {description}.");
			}

			await Task.Delay(100).ConfigureAwait(false);
		}
	}

	public async ValueTask DisposeAsync()
	{
		await Wallet.StopAsync(CancellationToken.None).ConfigureAwait(false);
		TransactionStore.Dispose();
		_filterStore.Dispose();
		_httpClientFactory.Dispose();
	}
}
