using NBitcoin;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using NBitcoin.RPC;
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
using WalletWasabi.WabiSabi.Models;
using static WalletWasabi.Services.Workers;

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

		var mempoolService = new MempoolService();
		var smartHeaderChain = new SmartHeaderChain(maxChainSize: 20_000);
		var networkWorkFolderPath = Path.Combine(DataDir, "BitcoinStore", Network.ToString());
		var fileSystemBlockRepository = new FileSystemBlockRepository(Path.Combine(networkWorkFolderPath, "Blocks"), Network);

		_allTransactionStore = new AllTransactionStore(networkWorkFolderPath, Network);
		_filterStore = new FilterStore(Path.Combine(networkWorkFolderPath, "IndexStore"), Network, smartHeaderChain);
		_ticker = new Timer(_ => EventBus.Publish(new Tick(DateTime.UtcNow)), 0, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(1));

		BitcoinStore = new BitcoinStore(_filterStore, _allTransactionStore, mempoolService, smartHeaderChain, fileSystemBlockRepository);

		ExternalSourcesHttpClientFactory = BuildHttpClientFactory();

		var nodesGroup = ConfigureBitcoinNetwork(mempoolService);
		NodesGroup = nodesGroup;
		var cpfpProvider = ConfigureCpfpInfoProvider();
		var blockProvider = ConfigureBlockProvider(nodesGroup, BitcoinStore.BlockRepository);

		var walletFactory = Wallet.CreateFactory(
			Config.Network,
			BitcoinStore,
			Config.ServiceConfiguration,
			blockProvider,
			EventBus,
			cpfpProvider);

		WalletManager = new WalletManager(Config.Network, DataDir, new WalletDirectories(Config.Network, DataDir), walletFactory);

		var broadcasters = CreateBroadcasters(nodesGroup);
		TransactionBroadcaster = new TransactionBroadcaster(broadcasters.ToArray(), BitcoinStore.MempoolService, WalletManager);

		Scheme = new Scheme(this);
	}

	private readonly AsyncLock _initializationAsyncLock = new();
	private readonly CancellationTokenSource _stoppingCts = new();

	private TorProcessManager? _torManager;
	private IRPCClient? _bitcoinRpcClient;
	private CoinPrison? _coinPrison;
	private readonly Timer _ticker;
	private readonly AllTransactionStore _allTransactionStore;
	private readonly FilterStore _filterStore;
	private readonly ComposedDisposable _disposables = new();

	public StatusContainer Status { get; }
	public string DataDir { get; }
	public TorSettings TorSettings { get; }
	public BitcoinStore BitcoinStore { get; }
	public IHttpClientFactory ExternalSourcesHttpClientFactory { get; }
	public Config Config { get; }
	public WalletManager WalletManager { get; }
	public NodesGroup NodesGroup { get; }
	public TransactionBroadcaster TransactionBroadcaster { get; set; }
	public HostedServices HostedServices { get; }
	public Network Network => Config.Network;
	public JsonRpcServer? RpcServer { get; private set; }
	public Uri? OnionServiceUri { get; private set; }
	public EventBus EventBus { get; }
	public Scheme Scheme { get; }

	private BlockProvider ConfigureBlockProvider(NodesGroup nodesGroup, FileSystemBlockRepository fileSystemBlockRepository)
	{
		var p2PNodesManager = new P2PNodesManager(Network, nodesGroup);
		var fileSystemBlockProvider = BlockProviders.FileSystemBlockProvider(fileSystemBlockRepository);
		var p2PBlockProvider = BlockProviders.P2pBlockProvider(p2PNodesManager);
		BlockProvider[] blockProviders = _bitcoinRpcClient is { } rpc
			? [fileSystemBlockProvider, BlockProviders.RpcBlockProvider(rpc), p2PBlockProvider]
			: [fileSystemBlockProvider, p2PBlockProvider];

		return BlockProviders.CachedBlockProvider(
			BlockProviders.ComposedBlockProvider(blockProviders),
			fileSystemBlockRepository);
	}

	private NodesGroup ConfigureBitcoinNetwork(MempoolService mempoolService)
	{
		var directory = Path.Combine(DataDir, "BitcoinP2pNetwork");
		var behavior = new P2pBehavior(mempoolService);
		var nodesGroup = Network == Network.RegTest
			? P2pNetwork.CreateNodesGroupForTestNet(behavior)
			: P2pNetwork.CreateNodesGroup(
				Network,
				Config.UseTor != TorMode.Disabled ? TorSettings.SocksEndpoint : null,
				directory,
				Config.BlockOnlyMode ? null : behavior);

		var serviceName = "Bitcoin Network Connectivity";
		var p2pNetwork = Spawn("BitcoinNetwork",
			Service(
				before: () => Logger.LogInfo($"Starting {serviceName}."),
				P2pNetwork.Create(nodesGroup, EventBus),
				after: () =>
				{
					Logger.LogInfo($"Stopped {serviceName}.");
					var addressManagerBehavior = nodesGroup.NodeConnectionParameters.TemplateBehaviors.Find<AddressManagerBehavior>();
					if (addressManagerBehavior is not null)
					{
						var addressManager = addressManagerBehavior.AddressManager;
						var addressManagerFilePath = Path.Combine(directory, $"AddressManager{Network}.dat");
						IoHelpers.EnsureContainingDirectoryExists(addressManagerFilePath);
						addressManager.SavePeerFile(addressManagerFilePath, Network);
						Logger.LogInfo($"{nameof(AddressManager)} is saved to `{addressManagerFilePath}`.");
					}
				}));
		p2pNetwork.DisposeUsing(_disposables);
		p2pNetwork.Post(Unit.Instance);

		return nodesGroup;
	}

	private void ConfigureBitcoinRpcClient()
	{
		var credentialString = Config.BitcoinRpcCredentialString;
		if (Config.UseBitcoinRpc && !string.IsNullOrWhiteSpace(credentialString))
		{
			// In case the credential string is malformed, we replace it with a valid but extremely improbable one.
			// That results in the creation of a rpc instance that will fail to connect. In that way the RpcMonitor
			// can detect the problem an inform to the user.
			if (!RPCCredentialString.TryParse(credentialString, out var credentials))
			{
				var improbableString = Convert.ToHexString(RandomUtils.GetBytes(32));
				credentials = RPCCredentialString.Parse($"{improbableString}:{improbableString}");
			}

			var bitcoinRpcUri = Config.BitcoinRpcUri;
			var internalRpcClient = new RPCClient(credentials, bitcoinRpcUri, Network);
			if (new Uri(bitcoinRpcUri).DnsSafeHost.EndsWith(".onion") && Config.UseTor != TorMode.Disabled)
			{
				internalRpcClient.HttpClient =
					ExternalSourcesHttpClientFactory.CreateClient("long-live-rpc-connection");
			}

			_bitcoinRpcClient = new RpcClientBase(internalRpcClient);
		}
	}

	private HttpClientFactory BuildHttpClientFactory(HttpClientHandlerConfiguration? config = null) =>
		Config.UseTor != TorMode.Disabled
			? new OnionHttpClientFactory(TorSettings.SocksEndpoint.ToUri("socks5"), config)
			: new HttpClientFactory(config);

	private void ConfigureFeeRateUpdater()
	{
		var blockFeeProvider = FeeRateProviders.BlockAsync(ExternalSourcesHttpClientFactory);
		var mempoolSpaceFeeProvider = FeeRateProviders.MempoolSpaceAsync(ExternalSourcesHttpClientFactory);
		var blockstreamInfoFeeProvider = FeeRateProviders.BlockstreamAsync(ExternalSourcesHttpClientFactory);
		FeeRateProvider feeRateProvider = Config.FeeRateEstimationProvider.ToLower() switch
		{
			"blockxyz" => FeeRateProviders.Composed([blockFeeProvider, mempoolSpaceFeeProvider, blockstreamInfoFeeProvider]),
			"mempoolspace" => FeeRateProviders.Composed([mempoolSpaceFeeProvider, blockstreamInfoFeeProvider]),
			"blockstreaminfo" => FeeRateProviders.Composed([blockstreamInfoFeeProvider, mempoolSpaceFeeProvider]),
			"" or "none" => FeeRateProviders.NoneAsync(),
			var providerName => throw new ArgumentException( $"Not supported fee rate estimations provider '{providerName}'. Default: '{Constants.DefaultFeeRateEstimationProvider}'")
		};

		if (_bitcoinRpcClient is not null)
		{
			var rpcFeeProvider = FeeRateProviders.RpcAsync(_bitcoinRpcClient);
			feeRateProvider = FeeRateProviders.Composed([rpcFeeProvider, feeRateProvider]);
		}
		var feeRateUpdater = Spawn("FeeRateUpdater",
			Service("Mining Fee Rate Updater",
				Periodically(
					TimeSpan.FromMinutes(15),
					FeeRateEstimations.Empty,
					FeeRateEstimationUpdater.CreateUpdater(feeRateProvider, EventBus))));
		feeRateUpdater.DisposeUsing(_disposables);
		EventBus.Subscribe<Tick>(_ => feeRateUpdater.Post(new FeeRateEstimationUpdater.UpdateMessage()));
	}

	private void ConfigureRpcMonitor()
	{
		if (_bitcoinRpcClient is not null)
		{
			var rpcMonitor = Spawn(RpcMonitor.ServiceName,
				Service("Bitcoin Rpc Interface Monitoring",
					Periodically(
						TimeSpan.FromSeconds(7),
						Unit.Instance,
						RpcMonitor.CreateChecker(_bitcoinRpcClient, EventBus))));
			rpcMonitor.DisposeUsing(_disposables);
			EventBus.Subscribe<Tick>(_ => rpcMonitor.Post(new RpcMonitor.CheckMessage()));
		}
	}

	private void ConfigureSynchronizer()
	{
		int maxFiltersToSync = Network == Network.Main ? 1000 : 10000; // On testnet, filters are empty, so it's faster to query them together
		var indexerHttpClientFactory = new IndexerHttpClientFactory(new Uri(Config.BackendUri), BuildHttpClientFactory());
		ICompactFilterProvider filtersProvider =
			new WebApiFilterProvider(maxFiltersToSync, indexerHttpClientFactory, EventBus);

		if (_bitcoinRpcClient is not null)
		{
			var supportsBlockFilters = _bitcoinRpcClient.SupportsBlockFiltersAsync(CancellationToken.None).GetAwaiter().GetResult();
			if (supportsBlockFilters)
			{
				filtersProvider = new BitcoinRpcFilterProvider(_bitcoinRpcClient);
			}
		}

		Spawn("Synchronizer",
			Service("Wasabi Index-Based Synchronizer",
				Continuously(
					Synchronizer.CreateFilterGenerator(filtersProvider, BitcoinStore, EventBus)
				)))
			.DisposeUsing(_disposables);
	}

	private void ConfigureExchangeRateUpdater()
	{
		var mempoolSpaceExchangeProvider = ExchangeRateProviders.MempoolSpaceAsync(ExternalSourcesHttpClientFactory);
		var blockstreamInfoExchangeProvider = ExchangeRateProviders.BlockstreamAsync(ExternalSourcesHttpClientFactory);
		var coinGeckoExchangeProvider = ExchangeRateProviders.CoinGeckoAsync(ExternalSourcesHttpClientFactory);
		var geminiExchangeProvider = ExchangeRateProviders.GeminiAsync(ExternalSourcesHttpClientFactory);
		ExchangeRateProvider exchangeRateProvider = Config.ExchangeRateProvider.ToLower() switch
		{
			"mempoolspace" => ExchangeRateProviders.Composed([mempoolSpaceExchangeProvider, blockstreamInfoExchangeProvider, coinGeckoExchangeProvider, geminiExchangeProvider ]),
			"blockstreaminfo" => ExchangeRateProviders.Composed([blockstreamInfoExchangeProvider, mempoolSpaceExchangeProvider, coinGeckoExchangeProvider, geminiExchangeProvider]),
			"coingecko" => ExchangeRateProviders.Composed([coinGeckoExchangeProvider, mempoolSpaceExchangeProvider, blockstreamInfoExchangeProvider, geminiExchangeProvider]),
			"gemini" => ExchangeRateProviders.Composed([geminiExchangeProvider, blockstreamInfoExchangeProvider, blockstreamInfoExchangeProvider, coinGeckoExchangeProvider, ]),
			"" or "none" => ExchangeRateProviders.NoneAsync(),
			var providerName => throw new ArgumentException( $"Not supported exchange rate provider '{providerName}'. Default: '{Constants.DefaultExchangeRateProvider}'")
		};

		var exchangeFeeRateUpdater = Spawn(ExchangeRateUpdater.ServiceName,
				Service("Exchange Rate Updater",
					Periodically(
						TimeSpan.FromMinutes(20),
						0m,
						ExchangeRateUpdater.CreateExchangeRateUpdater(exchangeRateProvider, EventBus))));
		exchangeFeeRateUpdater.DisposeUsing(_disposables);
		EventBus.Subscribe<Tick>(_ => exchangeFeeRateUpdater.Post(new ExchangeRateUpdater.UpdateMessage()));
	}

	private void ConfigureWasabiUpdater()
	{
		if (Config.UseTor is TorMode.Disabled)
		{
			Logger.LogInfo("Update manager requires Tor. Aborting...");
			return;
		}

		Uri[] relayUrls = [new ("wss://relay.primal.net"), new("wss://nos.lol"), new("wss://relay.damus.io")];
		var nostrClientFactory = () => NostrClientFactory.Create(relayUrls, TorSettings.SocksEndpoint);

		// The feature is disabled on linux at the moment because we install Wasabi Wallet as a Debian package.
		var installerDownloader = !Config.DownloadNewVersion
			? ReleaseDownloader.AutoDownloadOff()
			: RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && !PlatformInformation.IsDebianBasedOS()
				? ReleaseDownloader.ForUnsupportedLinuxDistributions()
				: ReleaseDownloader.ForOfficiallySupportedOSes(ExternalSourcesHttpClientFactory, EventBus);

		var wasabiVersionUpdater = Spawn("UpdateManager",
			Service("Wasabi Version AutoUpdater",
				Periodically(
					TimeSpan.FromHours(12),
					Unit.Instance,
					UpdateManager.CreateUpdater(nostrClientFactory, installerDownloader, EventBus))));
		wasabiVersionUpdater.DisposeUsing(_disposables);
		EventBus.Subscribe<Tick>(_ => wasabiVersionUpdater.Post(new UpdateManager.UpdateMessage()));
	}

	private CpfpInfoProvider ConfigureCpfpInfoProvider()
	{
		var cpfpUpdater = Spawn("CpfpInfoProvider",
			Service("External Cpfp Info provider",
				EventDriven(
					Unit.Instance,
					Network == Network.RegTest
					? CpfpInfoUpdater.CreateForRegTest()
					: CpfpInfoUpdater.Create(ExternalSourcesHttpClientFactory, Network, EventBus))));
		cpfpUpdater.DisposeUsing(_disposables);
		EventBus.Subscribe<FilterProcessed>(_ => cpfpUpdater.Post(new CpfpInfoMessage.UpdateMessage()));
		return new CpfpInfoProvider(cpfpUpdater);
	}

	public async Task InitializeAsync(bool initializeSleepInhibitor, TerminateService terminateService, CancellationToken cancellationToken)
	{
		ConfigureBitcoinRpcClient();
		ConfigureWasabiUpdater();
		ConfigureExchangeRateUpdater();
		ConfigureRpcMonitor();
		ConfigureFeeRateUpdater();
		ConfigureSynchronizer();

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
			_torManager = new TorProcessManager(TorSettings, EventBus);
			await _torManager.StartAsync(attempts: 3, cancellationToken).ConfigureAwait(false);
			Logger.LogInfo($"{nameof(TorProcessManager)} is initialized.");

			var (_, torControlClient) = await _torManager.WaitForNextAttemptAsync(cancellationToken).ConfigureAwait(false);
			if (Config is { JsonRpcServerEnabled: true, RpcOnionEnabled: true } && torControlClient is { } nonNullTorControlClient)
			{
				var anonymousAccessAllowed = string.IsNullOrEmpty(Config.JsonRpcUser) || string.IsNullOrEmpty(Config.JsonRpcPassword);
				if (!anonymousAccessAllowed)
				{
					var onionServiceId = await nonNullTorControlClient.CreateEphemeralOnionServiceAsync(80, 37129, cancellationToken).ConfigureAwait(false);
					OnionServiceUri = new Uri($"http://{onionServiceId}.onion");
					Logger.LogInfo($"RPC server listening on {OnionServiceUri}");
				}
				else
				{
					Logger.LogInfo("Anonymous access RPC server cannot be exposed as onion service.");
				}
			}

			var torStatusHttpClient = ExternalSourcesHttpClientFactory.CreateClient("long-live-torproject");
			var torStatusChecker = Spawn("TorStatusChecker",
				Periodically(
					TimeSpan.FromHours(1),
					Unit.Instance,
					TorStatusChecker.CreateChecker(torStatusHttpClient, EventBus)));
			torStatusChecker.DisposeUsing(_disposables);
			EventBus.Subscribe<Tick>(_ => torStatusChecker.Post(new TorStatusChecker.CheckMessage()));
		}
	}

	private void RegisterCoinJoinComponents(Uri coordinatorUri)
	{
		var prisonForCoordinator = Path.Combine(DataDir, coordinatorUri.Host);
		_coinPrison = CoinPrison.CreateOrLoadFromFile(prisonForCoordinator);

		EventBus
			.Subscribe<WalletLoaded>(e => _coinPrison.UpdateWallet(e.Wallet))
			.DisposeUsing(_disposables);

		// Aggressively retry
		var coordinatorHttpClientConfig = new HttpClientHandlerConfiguration
		{
			MaxAttempts = 10,
			TimeBeforeRetringAfterNetworkError = TimeSpan.FromSeconds(0.5),
			TimeBeforeRetringAfterServerError = TimeSpan.FromSeconds(0.5),
			TimeBeforeRetringAfterTooManyRequests = TimeSpan.FromSeconds(0.1)
		};
		var coordinatorHttpClientFactory = new CoordinatorHttpClientFactory(coordinatorUri, BuildHttpClientFactory(coordinatorHttpClientConfig));

		var wabiSabiStatusProvider =  new WabiSabiHttpApiClient("satoshi-coordination", coordinatorHttpClientFactory);
		var roundUpdater = Spawn("RoundUpdater",
			Service("WabiSabi Rounds Updater",
				EventDriven(
					new RoundsState(DateTime.UtcNow, RoundStateProvider.QueryFrequency, new Dictionary<uint256, RoundState>(), ImmutableList<RoundStateAwaiter>.Empty),
					RoundStateUpdater.Create(wabiSabiStatusProvider))));
		roundUpdater.DisposeUsing(_disposables);
		EventBus.Subscribe<Tick>(_ => roundUpdater.Post(new RoundUpdateMessage.UpdateMessage(DateTime.UtcNow)));

		Func<string, WabiSabiHttpApiClient> wabiSabiHttpClientFactory = (identity) => new WabiSabiHttpApiClient(identity, coordinatorHttpClientFactory!);
		var coinJoinConfiguration = new CoinJoinConfiguration(Config.CoordinatorIdentifier, Config.MaxCoinjoinMiningFeeRate, Config.AbsoluteMinInputCount, AllowSoloCoinjoining: false);
		HostedServices.Register<CoinJoinManager>(() => new CoinJoinManager(WalletManager, new RoundStateProvider(roundUpdater), wabiSabiHttpClientFactory, coinJoinConfiguration, _coinPrison, EventBus), "CoinJoin Manager");
	}

	private List<IBroadcaster> CreateBroadcasters(NodesGroup nodesGroup)
	{
		var broadcasters = new List<IBroadcaster>();
		if (_bitcoinRpcClient is not null)
		{
			broadcasters.Add(new RpcBroadcaster(_bitcoinRpcClient));
		}

		broadcasters.AddRange([
			new NetworkBroadcaster(BitcoinStore.MempoolService, nodesGroup),
			new ExternalTransactionBroadcaster(Config.ExternalTransactionBroadcaster, Network, ExternalSourcesHttpClientFactory),
		]);

		return broadcasters;
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
			Logger.LogWarning("Process is exiting.");

			try
			{
				try
				{
					using var dequeueCts = new CancellationTokenSource(TimeSpan.FromMinutes(6));
					await WalletManager.RemoveAndStopAllAsync(dequeueCts.Token).ConfigureAwait(false);
					Logger.LogInfo($"{nameof(WalletManager)} is stopped.");
				}
				catch (Exception ex)
				{
					Logger.LogError($"Error during {nameof(WalletManager.RemoveAndStopAllAsync)}: {ex}");
				}

				Status.Dispose();

				NodesGroup.Dispose();

				_disposables.Dispose();

				if (_coinPrison is { } coinPrison)
				{
					coinPrison.Dispose();
				}

				if (RpcServer is { } rpcServer)
				{
					using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(21));
					await rpcServer.StopAsync(cts.Token).ConfigureAwait(false);
					Logger.LogInfo($"{nameof(RpcServer)} is stopped.");
				}

				if (HostedServices is { } backgroundServices)
				{
					using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(21));
					await backgroundServices.StopAllAsync(cts.Token).ConfigureAwait(false);
					backgroundServices.Dispose();
					Logger.LogInfo("Stopped background services.");
				}

				if (_torManager is { } torManager)
				{
					using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
					var (_, torControlClient) = await _torManager.WaitForNextAttemptAsync(cts.Token).ConfigureAwait(false);
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
					Logger.LogInfo("TorManager is stopped.");
				}

				try
				{
					await _filterStore.DisposeAsync().ConfigureAwait(false);
					Logger.LogInfo($"{nameof(FilterStore)} is disposed.");

					await _allTransactionStore.DisposeAsync().ConfigureAwait(false);
					Logger.LogInfo($"{nameof(AllTransactionStore)} is disposed.");
				}
				catch (Exception ex)
				{
					Logger.LogError($"Error during the disposal of {nameof(FilterStore)} and {nameof(AllTransactionStore)}: {ex}");
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
