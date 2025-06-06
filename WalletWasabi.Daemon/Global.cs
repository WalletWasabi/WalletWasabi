using Microsoft.Extensions.Caching.Memory;
using NBitcoin;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin.RPC;
using NNostr.Client;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.BitcoinP2p;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Discoverability;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Rpc;
using WalletWasabi.Services;
using WalletWasabi.Services.Terminate;
using WalletWasabi.Stores;
using WalletWasabi.Tor;
using WalletWasabi.Tor.StatusChecker;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.Banning;
using WalletWasabi.WabiSabi.Client.RoundStateAwaiters;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;
using WalletWasabi.Models;
using WalletWasabi.Wallets.Exchange;
using WalletWasabi.FeeRateEstimation;

namespace WalletWasabi.Daemon;

public class Global
{
	/// <remarks>Use this variable as a guard to prevent touching <see cref="_stoppingCts"/> that might have already been disposed.</remarks>
	private volatile bool _disposeRequested;

	public Global(string dataDir, Config config)
	{
		DataDir = dataDir;
		Config = config;
		TorSettings = new TorSettings(
			DataDir,
			distributionFolderPath: EnvironmentHelpers.GetFullBaseDirectory(),
			terminateOnExit: Config.TerminateTorOnExit,
			torMode: Config.UseTor,
			socksPort: config.TorSocksPort,
			controlPort: config.TorControlPort,
			torFolder: config.TorFolder,
			bridges: config.TorBridges,
			owningProcessId: Environment.ProcessId,
			log: Config.LogModes.Contains(LogMode.File));

		EventBus = new EventBus();
		Status = new StatusContainer(EventBus, installOnClose: Config.DownloadNewVersion);
		HostedServices = new HostedServices();

		var networkWorkFolderPath = Path.Combine(DataDir, "BitcoinStore", Network.ToString());
		_allTransactionStore = new AllTransactionStore(networkWorkFolderPath, Network);
		SmartHeaderChain smartHeaderChain = new(maxChainSize: 20_000);
		_indexStore = new IndexStore(Path.Combine(networkWorkFolderPath, "IndexStore"), Network, smartHeaderChain);
		var mempoolService = new MempoolService();
		var fileSystemBlockRepository = new FileSystemBlockRepository(Path.Combine(networkWorkFolderPath, "Blocks"), Network);

		BitcoinStore = new BitcoinStore(_indexStore, _allTransactionStore, mempoolService, smartHeaderChain, fileSystemBlockRepository);

		ExternalSourcesHttpClientFactory = BuildHttpClientFactory();
		BackendHttpClientFactory = new IndexerHttpClientFactory(new Uri(config.BackendUri), BuildHttpClientFactory());

		if (config.UseTor != TorMode.Disabled)
		{
			Uri[] relayUrls = [new("wss://relay.primal.net"), new("wss://nos.lol"), new("wss://relay.damus.io")];
			var nostrClientFactory = () => NostrClientFactory.Create(relayUrls, TorSettings.SocksEndpoint);

			// The feature is disabled on linux at the moment because we install Wasabi Wallet as a Debian package.
			var installerDownloader = !Config.DownloadNewVersion
				? ReleaseDownloader.AutoDownloadOff()
				: RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && !PlatformInformation.IsDebianBasedOS()
					? ReleaseDownloader.ForUnsupportedLinuxDistributions()
					: ReleaseDownloader.ForOfficiallySupportedOSes(ExternalSourcesHttpClientFactory, EventBus);

			HostedServices.Register<UpdateManager>(
				() => new UpdateManager(TimeSpan.FromDays(1), nostrClientFactory, installerDownloader, EventBus),
				"Update Manager");
			UpdateManager = HostedServices.Get<UpdateManager>();
		}

		TorStatusChecker = new TorStatusChecker(TimeSpan.FromHours(6), ExternalSourcesHttpClientFactory.CreateClient("long-live-torproject"), EventBus);

		Cache = new MemoryCache(new MemoryCacheOptions
		{
			SizeLimit = 1_000,
			ExpirationScanFrequency = TimeSpan.FromSeconds(30)
		});

		// Register P2P network.
		HostedServices.Register<P2pNetwork>(
			() =>
			{
				var p2p = new P2pNetwork(
						Network,
						Config.UseTor != TorMode.Disabled ? TorSettings.SocksEndpoint : null,
						Path.Combine(DataDir, "BitcoinP2pNetwork"),
						BitcoinStore);
				if (!Config.BlockOnlyMode)
				{
					p2p.Nodes.NodeConnectionParameters.TemplateBehaviors.Add(BitcoinStore.CreateUntrustedP2pBehavior());
				}

				return p2p;
			},
			friendlyName: "Bitcoin P2P Network");

		HostedServices.Register<ExchangeRateUpdater>(() => new ExchangeRateUpdater(TimeSpan.FromMinutes(5), ()=> Config.ExchangeRateProvider, ExternalSourcesHttpClientFactory, EventBus), "Exchange rate updater");

		FeeRateProvider feeRateProvider = config.FeeRateEstimationProvider.ToLower() switch
		{
			"mempoolspace" => FeeRateProviders.MempoolSpaceAsync(ExternalSourcesHttpClientFactory),
			"blockstreaminfo" => FeeRateProviders.BlockstreamAsync(ExternalSourcesHttpClientFactory),
			"" or "none" => FeeRateProviders.NoneAsync(),
			var providerName => throw new ArgumentException( $"Not supported fee rate estimations provider '{providerName}'. Default: '{Constants.DefaultFeeRateEstimationProvider}'")
		};

		// Block providers.
		_p2PNodesManager = new P2PNodesManager(Network, HostedServices.Get<P2pNetwork>().Nodes);

		TimeSpan requestInterval = Network == Network.RegTest ? TimeSpan.FromSeconds(5) : TimeSpan.FromSeconds(30);
		int maxFiltersToSync = Network == Network.Main ? 1000 : 10000; // On testnet, filters are empty, so it's faster to query them together
		ICompactFilterProvider filtersProvider =
			new WebApiFilterProvider(maxFiltersToSync, BackendHttpClientFactory, EventBus);

		var credentialString = config.BitcoinRpcCredentialString;
		if (config.UseBitcoinRpc && !string.IsNullOrWhiteSpace(credentialString))
		{
			var bitcoinRpcUri = config.BitcoinRpcUri;
			var internalRpcClient = new RPCClient(credentialString, bitcoinRpcUri, Network);
			if (new Uri(bitcoinRpcUri).DnsSafeHost.EndsWith(".onion") && Config.UseTor != TorMode.Disabled)
			{
				internalRpcClient.HttpClient = ExternalSourcesHttpClientFactory.CreateClient("long-live-rpc-connection");
			}
			BitcoinRpcClient = new RpcClientBase(internalRpcClient);
			HostedServices.Register<RpcMonitor>(() => new RpcMonitor(TimeSpan.FromSeconds(7), BitcoinRpcClient, EventBus), "RPC Monitor");

			var supportsBlockFilters = BitcoinRpcClient.SupportsBlockFiltersAsync(CancellationToken.None).GetAwaiter().GetResult();
			if (supportsBlockFilters)
			{
				filtersProvider = new BitcoinRpcFilterProvider(BitcoinRpcClient);
			}

			var rpcFeeProvider = FeeRateProviders.RpcAsync(BitcoinRpcClient);
			feeRateProvider = FeeRateProviders.Composed([rpcFeeProvider, feeRateProvider]);
		}

		HostedServices.Register<FeeRateEstimationUpdater>(() => new FeeRateEstimationUpdater(TimeSpan.FromMinutes(5), feeRateProvider, EventBus), "Mining Fee rate estimations updater");
		HostedServices.Register<Synchronizer>(() => new Synchronizer(requestInterval, filtersProvider, BitcoinStore, EventBus), "Wasabi Synchronizer");

		var fileSystemBlockProvider = BlockProviders.FileSystemBlockProvider(fileSystemBlockRepository);
		var p2PBlockProvider = BlockProviders.P2pBlockProvider(_p2PNodesManager);
		BlockProvider[] blockProviders = BitcoinRpcClient is { } rpc
			? [fileSystemBlockProvider, BlockProviders.RpcBlockProvider(rpc), p2PBlockProvider]
			: [fileSystemBlockProvider, p2PBlockProvider];

		var blockProvider = BlockProviders.CachedBlockProvider(
			BlockProviders.ComposedBlockProvider(blockProviders),
			fileSystemBlockRepository);

		if (Network != Network.RegTest)
		{
			HostedServices.Register<CpfpInfoProvider>(() => new CpfpInfoProvider(ExternalSourcesHttpClientFactory, Network), friendlyName: "CPFP Info Provider");
		}

		WalletFactory walletFactory = new(
			config.Network,
			BitcoinStore,
			config.ServiceConfiguration,
			HostedServices.Get<FeeRateEstimationUpdater>(),
			blockProvider,
			EventBus,
			Network == Network.RegTest ? null : HostedServices.Get<CpfpInfoProvider>());

		WalletManager = new WalletManager(config.Network, DataDir, new WalletDirectories(Config.Network, DataDir), walletFactory);

		var broadcasters = CreateBroadcasters();
		TransactionBroadcaster = new TransactionBroadcaster(broadcasters.ToArray(), BitcoinStore.MempoolService, WalletManager);

		WalletManager.WalletStateChanged += WalletManager_WalletStateChanged;
	}

	public StatusContainer Status { get; }

	/// <summary>Lock that makes sure the application initialization and dispose methods do not run concurrently.</summary>
	private readonly AsyncLock _initializationAsyncLock = new();

	/// <summary>Cancellation token to cancel <see cref="InitializeNoWalletAsync(TerminateService)"/> processing.</summary>
	private readonly CancellationTokenSource _stoppingCts = new();

	public string DataDir { get; }
	public TorSettings TorSettings { get; }
	public BitcoinStore BitcoinStore { get; }

	public IHttpClientFactory ExternalSourcesHttpClientFactory { get; }
	public IHttpClientFactory BackendHttpClientFactory { get; }
	public IHttpClientFactory? CoordinatorHttpClientFactory { get; set; }
	public Config Config { get; }
	public WalletManager WalletManager { get; }
	public TransactionBroadcaster TransactionBroadcaster { get; set; }
	private readonly P2PNodesManager _p2PNodesManager;
	private TorProcessManager? TorManager { get; set; }
	public TorStatusChecker TorStatusChecker { get; set; }
	public IRPCClient? BitcoinRpcClient { get; }
	public UpdateManager? UpdateManager { get; }
	public HostedServices HostedServices { get; }
	public Network Network => Config.Network;

	public IMemoryCache Cache { get; private set; }
	public CoinPrison? CoinPrison { get; private set; }
	public JsonRpcServer? RpcServer { get; private set; }

	public Uri? OnionServiceUri { get; private set; }

	public EventBus EventBus { get; }

	private readonly AllTransactionStore _allTransactionStore;
	private readonly IndexStore _indexStore;

	private HttpClientFactory BuildHttpClientFactory() =>
		Config.UseTor != TorMode.Disabled
			? new OnionHttpClientFactory(TorSettings.SocksEndpoint.ToUri("socks5"))
			: new HttpClientFactory();
	public async Task InitializeNoWalletAsync(bool initializeSleepInhibitor, TerminateService terminateService, CancellationToken cancellationToken)
	{
		using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stoppingCts.Token);
		CancellationToken cancel = linkedCts.Token;

		// _stoppingCts may be disposed at this point, so do not forward the cancellation token here.
		using (await _initializationAsyncLock.LockAsync(cancellationToken))
		{
			Logger.LogTrace("Initialization started.");

			if (_disposeRequested)
			{
				return;
			}

			try
			{
				var bitcoinStoreInitTask = BitcoinStore.InitializeAsync(cancel);

				cancel.ThrowIfCancellationRequested();

				await StartTorProcessManagerAsync(cancel).ConfigureAwait(false);

				try
				{
					await bitcoinStoreInitTask.ConfigureAwait(false);

					// Make sure that the height of the wallets will not be better than the current height of the filters.
					WalletManager.SetMaxBestHeight(BitcoinStore.SmartHeaderChain.TipHeight);
				}
				catch (Exception ex) when (ex is not OperationCanceledException)
				{
					// If our internal data structures in the Bitcoin Store gets corrupted, then it's better to rescan all the wallets.
					WalletManager.SetMaxBestHeight(SmartHeader.GetStartingHeader(Network).Height);
					throw;
				}

				if (Config.TryGetCoordinatorUri(out var coordinatorUri))
				{
					RegisterCoinJoinComponents(coordinatorUri);

					if (initializeSleepInhibitor)
					{
						await CreateSleepInhibitorAsync().ConfigureAwait(false);
					}
				}

				await HostedServices.StartAllAsync(cancel).ConfigureAwait(false);

				Logger.LogInfo("Start synchronizing filters...");

				await StartRpcServerAsync(terminateService, cancel).ConfigureAwait(false);

				WalletManager.Initialize();
			}
			finally
			{
				Logger.LogTrace("Initialization finished.");
			}
		}
	}

	private async Task CreateSleepInhibitorAsync()
	{
		SleepInhibitor? sleepInhibitor = await SleepInhibitor.CreateAsync(HostedServices.Get<CoinJoinManager>()).ConfigureAwait(false);

		if (sleepInhibitor is not null)
		{
			HostedServices.Register<SleepInhibitor>(() => sleepInhibitor, "Sleep Inhibitor");
		}
		else
		{
			Logger.LogInfo("Sleep Inhibitor is not available on this platform.");
		}
	}

	private async Task StartRpcServerAsync(TerminateService terminateService, CancellationToken cancel)
	{
		// HttpListener doesn't support onion services as prefix and for that reason we have no alternative
		// other than using
		var prefixes = OnionServiceUri is { }
			? Config.JsonRpcServerPrefixes.Append($"http://+:37129/").ToArray()
			: Config.JsonRpcServerPrefixes;

		var jsonRpcServerConfig = new JsonRpcServerConfiguration(Config.JsonRpcServerEnabled, Config.JsonRpcUser, Config.JsonRpcPassword, prefixes, Config.Network);
		if (jsonRpcServerConfig.IsEnabled)
		{
			var wasabiJsonRpcService = new Rpc.WasabiJsonRpcService(global: this);
			RpcServer = new JsonRpcServer(wasabiJsonRpcService, jsonRpcServerConfig, terminateService);
			try
			{
				await RpcServer.StartAsync(cancel).ConfigureAwait(false);
			}
			catch (HttpListenerException e)
			{
				Logger.LogWarning($"Failed to start {nameof(JsonRpcServer)} with error: {e.Message}.");
				RpcServer = null;
			}
		}
	}

	private async Task StartTorProcessManagerAsync(CancellationToken cancellationToken)
	{
		if (Config.UseTor != TorMode.Disabled)
		{
			TorManager = new TorProcessManager(TorSettings, EventBus);
			await TorManager.StartAsync(attempts: 3, cancellationToken).ConfigureAwait(false);
			Logger.LogInfo($"{nameof(TorProcessManager)} is initialized.");

			var (_, torControlClient) = await TorManager.WaitForNextAttemptAsync(cancellationToken).ConfigureAwait(false);
			if (Config is { JsonRpcServerEnabled: true, RpcOnionEnabled: true } && torControlClient is { } nonNullTorControlClient)
			{
				var anonymousAccessAllowed = string.IsNullOrEmpty(Config.JsonRpcUser) || string.IsNullOrEmpty(Config.JsonRpcPassword);
				if (!anonymousAccessAllowed)
				{
					var onionServiceId = await nonNullTorControlClient.CreateOnionServiceAsync(TorSettings.RpcVirtualPort, TorSettings.RpcOnionPort, cancellationToken).ConfigureAwait(false);
					OnionServiceUri = new Uri($"http://{onionServiceId}.onion");
					Logger.LogInfo($"RPC server listening on {OnionServiceUri}");
				}
				else
				{
					Logger.LogInfo("Anonymous access RPC server cannot be exposed as onion service.");
				}
			}

			HostedServices.Register<TorStatusChecker>(() => TorStatusChecker, "Tor Network Checker");
		}
	}

	private void RegisterCoinJoinComponents(Uri coordinatorUri)
	{
		var prisonForCoordinator = Path.Combine(DataDir, coordinatorUri.Host);
		CoinPrison = CoinPrison.CreateOrLoadFromFile(prisonForCoordinator);

		CoordinatorHttpClientFactory = new CoordinatorHttpClientFactory(coordinatorUri, BuildHttpClientFactory());

		var wabiSabiStatusProvider =  new WabiSabiHttpApiClient("satoshi-coordination", CoordinatorHttpClientFactory);
		HostedServices.Register<RoundStateUpdater>(() => new RoundStateUpdater(TimeSpan.FromSeconds(10), wabiSabiStatusProvider), "Round info updater");

		Func<string, WabiSabiHttpApiClient> wabiSabiHttpClientFactory = (identity) => new WabiSabiHttpApiClient(identity, CoordinatorHttpClientFactory!);
		var coinJoinConfiguration = new CoinJoinConfiguration(Config.CoordinatorIdentifier, Config.MaxCoinjoinMiningFeeRate, Config.AbsoluteMinInputCount, AllowSoloCoinjoining: false);
		HostedServices.Register<CoinJoinManager>(() => new CoinJoinManager(WalletManager, HostedServices.Get<RoundStateUpdater>(), wabiSabiHttpClientFactory, coinJoinConfiguration, CoinPrison, EventBus), "CoinJoin Manager");
	}

	private List<IBroadcaster> CreateBroadcasters()
	{
		var p2p = HostedServices.Get<P2pNetwork>();
		var broadcasters = new List<IBroadcaster>();
		if (BitcoinRpcClient is not null)
		{
			broadcasters.Add(new RpcBroadcaster(BitcoinRpcClient));
		}

		broadcasters.AddRange([
			new NetworkBroadcaster(BitcoinStore.MempoolService, p2p.Nodes),
			new ExternalTransactionBroadcaster(Config.ExternalTransactionBroadcaster, Network, ExternalSourcesHttpClientFactory),
		]);

		return broadcasters;
	}

	private void WalletManager_WalletStateChanged(object? sender, WalletState e)
	{
		// Load banned coins in wallet.
		// This event function can be deleted later when SmartCoin.IsBanned is removed.
		if (e is not WalletState.Started)
		{
			return;
		}

		var wallet = sender as Wallet ?? throw new InvalidOperationException($"The sender for {nameof(WalletManager.WalletStateChanged)} was not a Wallet.");
		CoinPrison?.UpdateWallet(wallet);
	}

	public async Task DisposeAsync()
	{
		// Dispose method may be called just once.
		if (!_disposeRequested)
		{
			_disposeRequested = true;
			_stoppingCts.Cancel();
		}
		else
		{
			return;
		}

		using (await _initializationAsyncLock.LockAsync())
		{
			Logger.LogWarning("Process is exiting.", nameof(Global));

			try
			{
				try
				{
					using var dequeueCts = new CancellationTokenSource(TimeSpan.FromMinutes(6));
					await WalletManager.RemoveAndStopAllAsync(dequeueCts.Token).ConfigureAwait(false);
					WalletManager.WalletStateChanged -= WalletManager_WalletStateChanged;
					Logger.LogInfo($"{nameof(WalletManager)} is stopped.", nameof(Global));
				}
				catch (Exception ex)
				{
					Logger.LogError($"Error during {nameof(WalletManager.RemoveAndStopAllAsync)}: {ex}");
				}

				Status.Dispose();

				if (CoinPrison is { } coinPrison)
				{
					coinPrison.Dispose();
				}

				if (RpcServer is { } rpcServer)
				{
					using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(21));
					await rpcServer.StopAsync(cts.Token).ConfigureAwait(false);
					Logger.LogInfo($"{nameof(RpcServer)} is stopped.", nameof(Global));
				}

				if (HostedServices is { } backgroundServices)
				{
					using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(21));
					await backgroundServices.StopAllAsync(cts.Token).ConfigureAwait(false);
					backgroundServices.Dispose();
					Logger.LogInfo("Stopped background services.");
				}

				if (TorStatusChecker is { } torStatusChecker)
				{
					torStatusChecker.Dispose();
					Logger.LogInfo($"{nameof(TorStatusChecker)} is stopped.");
				}

				if (TorManager is { } torManager)
				{
					using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
					var (_, torControlClient) = await TorManager.WaitForNextAttemptAsync(cts.Token).ConfigureAwait(false);
					if (OnionServiceUri is { } nonNullOnionServiceUri && torControlClient is { } nonNullTorControlClient)
					{
						try
						{
							var isDestroyedSuccessfully = await nonNullTorControlClient
								.DestroyOnionServiceAsync(nonNullOnionServiceUri.Host, cts.Token).ConfigureAwait(false);
							if (!isDestroyedSuccessfully)
							{
								Logger.LogInfo($"Onion service '{nonNullOnionServiceUri.Host}' failed to be destroyed.");
							}
						}
						catch (OperationCanceledException)
						{
							Logger.LogInfo($"'{nonNullOnionServiceUri.Host}' failed to be stopped in allotted time.");
						}
					}

					await torManager.DisposeAsync().ConfigureAwait(false);
					Logger.LogInfo($"{nameof(TorManager)} is stopped.");
				}

				if (Cache is { } cache)
				{
					cache.Dispose();
					Logger.LogInfo($"{nameof(Cache)} is disposed.");
				}

				try
				{
					await _indexStore.DisposeAsync().ConfigureAwait(false);
					Logger.LogInfo($"{nameof(IndexStore)} is disposed.");

					await _allTransactionStore.DisposeAsync().ConfigureAwait(false);
					Logger.LogInfo($"{nameof(AllTransactionStore)} is disposed.");
				}
				catch (Exception ex)
				{
					Logger.LogError($"Error during the disposal of {nameof(IndexStore)} and {nameof(AllTransactionStore)}: {ex}");
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}
			finally
			{
				_stoppingCts.Dispose();
				Logger.LogTrace("Dispose finished.");
			}
		}
	}
}
