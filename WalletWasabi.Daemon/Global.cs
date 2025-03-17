using Microsoft.Extensions.Caching.Memory;
using NBitcoin;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore;
using WalletWasabi.BitcoinCore.Endpointing;
using WalletWasabi.BitcoinCore.Monitoring;
using WalletWasabi.BitcoinP2p;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Blockchain.Transactions;
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
using WalletWasabi.Wallets.FilterProcessor;
using WalletWasabi.Models;
using WalletWasabi.Wallets.Exchange;
using WalletWasabi.FeeRateEstimation;
using WalletWasabi.WebClients;

namespace WalletWasabi.Daemon;

public class Global
{
	/// <remarks>Use this variable as a guard to prevent touching <see cref="_stoppingCts"/> that might have already been disposed.</remarks>
	private volatile bool _disposeRequested;

	public Global(string dataDir, string configFilePath, Config config)
	{
		DataDir = dataDir;
		ConfigFilePath = configFilePath;
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

		HostedServices = new HostedServices();

		var networkWorkFolderPath = Path.Combine(DataDir, "BitcoinStore", Network.ToString());
		_allTransactionStore = new AllTransactionStore(networkWorkFolderPath, Network);
		SmartHeaderChain smartHeaderChain = new(maxChainSize: 20_000);
		_indexStore = new IndexStore(Path.Combine(networkWorkFolderPath, "IndexStore"), Network, smartHeaderChain);
		var mempoolService = new MempoolService();
		var blocks = new FileSystemBlockRepository(Path.Combine(networkWorkFolderPath, "Blocks"), Network);

		BitcoinStore = new BitcoinStore(_indexStore, _allTransactionStore, mempoolService, smartHeaderChain, blocks);

		ExternalSourcesHttpClientFactory = BuildHttpClientFactory();
		BackendHttpClientFactory = new BackendHttpClientFactory(Config.GetBackendUri(), BuildHttpClientFactory());

		var wasabiNostrClient = new WasabiNostrClient(TorSettings.SocksEndpoint);
		HostedServices.Register<UpdateManager>(() => new UpdateManager(TimeSpan.FromDays(1), DataDir, Config.DownloadNewVersion, ExternalSourcesHttpClientFactory.CreateClient("long-live-wasabiwallet.io"), wasabiNostrClient), "Update Manager");
		UpdateManager = HostedServices.Get<UpdateManager>();

		TimeSpan requestInterval = Network == Network.RegTest ? TimeSpan.FromSeconds(5) : TimeSpan.FromSeconds(30);
		int maxFiltersToSync = Network == Network.Main ? 1000 : 10000; // On testnet, filters are empty, so it's faster to query them together

		HostedServices.Register<WasabiSynchronizer>(() => new WasabiSynchronizer(requestInterval, maxFiltersToSync, BitcoinStore, BackendHttpClientFactory), "Wasabi Synchronizer");
		WasabiSynchronizer wasabiSynchronizer = HostedServices.Get<WasabiSynchronizer>();

		TorStatusChecker = new TorStatusChecker(TimeSpan.FromHours(6), ExternalSourcesHttpClientFactory.CreateClient("long-live-torproject"), new XmlIssueListParser());

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
						Config.GetBitcoinP2pEndPoint(),
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

		RegisterExchangeRateProviders();
		RegisterFeeRateProviders();

		// Block providers.
		var torEndpoint = TorSettings.TorMode != TorMode.Disabled ? TorSettings.SocksEndpoint : null;
		_specificNodeBlockProvider = new SpecificNodeBlockProvider(Network, Config.ServiceConfiguration, torEndpoint);
		_p2PNodesManager = new P2PNodesManager(Network, HostedServices.Get<P2pNetwork>().Nodes);

		IBlockProvider[] trustedFullNodeBlockProviders = BitcoinCoreNode?.RpcClient is null
			? [_specificNodeBlockProvider]
			: [new RpcBlockProvider(BitcoinCoreNode.RpcClient), _specificNodeBlockProvider];

		_blockDownloadService = new BlockDownloadService(
			fileSystemBlockRepository: BitcoinStore.BlockRepository,
			trustedFullNodeBlockProviders: trustedFullNodeBlockProviders,
			new P2PBlockProvider(_p2PNodesManager));

		if (Network != Network.RegTest)
		{
			HostedServices.Register<CpfpInfoProvider>(() => new CpfpInfoProvider(ExternalSourcesHttpClientFactory, Network), friendlyName: "CPFP Info Provider");
		}

		WalletFactory walletFactory = new(
			DataDir,
			config.Network,
			BitcoinStore,
			wasabiSynchronizer,
			config.ServiceConfiguration,
			HostedServices.Get<FeeRateEstimationUpdater>(),
			_blockDownloadService,
			Network == Network.RegTest ? null : HostedServices.Get<CpfpInfoProvider>());

		WalletManager = new WalletManager(config.Network, DataDir, new WalletDirectories(Config.Network, DataDir), walletFactory);
		var p2p = HostedServices.Get<P2pNetwork>();
		var broadcasters = new List<IBroadcaster>();
		if (BitcoinCoreNode?.RpcClient is not null)
		{
			broadcasters.Add(new RpcBroadcaster(BitcoinCoreNode.RpcClient));
		}
		broadcasters.AddRange([
			new NetworkBroadcaster(BitcoinStore.MempoolService, p2p.Nodes),
			new BackendBroadcaster(BackendHttpClientFactory)
		]);

		TransactionBroadcaster = new TransactionBroadcaster(broadcasters.ToArray(), BitcoinStore.MempoolService, WalletManager);

		WalletManager.WalletStateChanged += WalletManager_WalletStateChanged;
	}

	public const string ThemeBackgroundBrushResourceKey = "ThemeBackgroundBrush";
	public const string ApplicationAccentForegroundBrushResourceKey = "ApplicationAccentForegroundBrush";

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

	public string ConfigFilePath { get; }
	public Config Config { get; }
	public WalletManager WalletManager { get; }
	public TransactionBroadcaster TransactionBroadcaster { get; set; }
	private readonly SpecificNodeBlockProvider _specificNodeBlockProvider;
	private readonly BlockDownloadService _blockDownloadService;
	private readonly P2PNodesManager _p2PNodesManager;
	private TorProcessManager? TorManager { get; set; }
	public CoreNode? BitcoinCoreNode { get; private set; }
	public TorStatusChecker TorStatusChecker { get; set; }
	public UpdateManager UpdateManager { get; }
	public HostedServices HostedServices { get; }

	public Network Network => Config.Network;

	public IMemoryCache Cache { get; private set; }
	public CoinPrison? CoinPrison { get; private set; }
	public JsonRpcServer? RpcServer { get; private set; }

	public Uri? OnionServiceUri { get; private set; }

	private readonly AllTransactionStore _allTransactionStore;
	private readonly IndexStore _indexStore;

	private HttpClientFactory BuildHttpClientFactory() =>
		Config.UseTor != TorMode.Disabled
			? new OnionHttpClientFactory(new Uri($"socks5://{TorSettings.SocksEndpoint.ToEndpointString()}"))
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

					// Make sure that TurboSyncHeight is not higher than BestHeight
					WalletManager.EnsureTurboSyncHeightConsistency();

					// Make sure that the height of the wallets will not be better than the current height of the filters.
					WalletManager.SetMaxBestHeight(BitcoinStore.SmartHeaderChain.TipHeight);
				}
				catch (Exception ex) when (ex is not OperationCanceledException)
				{
					// If our internal data structures in the Bitcoin Store gets corrupted, then it's better to rescan all the wallets.
					WalletManager.SetMaxBestHeight(SmartHeader.GetStartingHeader(Network).Height);
					throw;
				}

				await StartLocalBitcoinNodeAsync(cancel).ConfigureAwait(false);

				await _blockDownloadService.StartAsync(cancel).ConfigureAwait(false);

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
			TorManager = new TorProcessManager(TorSettings);
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

	private async Task StartLocalBitcoinNodeAsync(CancellationToken cancel)
	{
		try
		{
			if (Config.StartLocalBitcoinCoreOnStartup)
			{
				CoreNodeParams coreNodeParams = new(
					Network,
					BitcoinStore.MempoolService,
					Config.LocalBitcoinCoreDataDir,
					tryRestart: false,
					tryDeleteDataDir: false,
					EndPointStrategy.Default(Network, EndPointType.P2p),
					EndPointStrategy.Default(Network, EndPointType.Rpc),
					txIndex: null,
					prune: null,
					disableWallet: 1,
					mempoolReplacement: null,
					userAgent: $"/WasabiClient:{Constants.ClientVersion}/",
					fallbackFee: null, // ToDo: Maybe we should have it, not only for tests?
					Cache);

				CoreNode coreNode = await CoreNode.CreateAsync(coreNodeParams, cancel).ConfigureAwait(false);

				RegisterLocalNodeDependentComponents(coreNode);
				BitcoinCoreNode = coreNode;
			}
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
		}
	}

	private void RegisterLocalNodeDependentComponents(CoreNode coreNode)
	{
		HostedServices.Register<BlockNotifier>(() => new BlockNotifier(coreNode.RpcClient, coreNode.P2pNode), "Block Notifier");
		HostedServices.Register<RpcMonitor>(() => new RpcMonitor(TimeSpan.FromSeconds(7), coreNode.RpcClient), "RPC Monitor");
	}

	private void RegisterFeeRateProviders()
	{
		HostedServices.Register<FeeRateEstimationUpdater>(() => new FeeRateEstimationUpdater(TimeSpan.FromMinutes(5), ()=> Config.FeeRateEstimationProvider, ExternalSourcesHttpClientFactory), "Exchange rate updater");
	}

	private void RegisterExchangeRateProviders()
	{
		HostedServices.Register<ExchangeRateUpdater>(() => new ExchangeRateUpdater(TimeSpan.FromMinutes(5), ()=> Config.ExchangeRateProvider, ExternalSourcesHttpClientFactory), "Exchange rate updater");
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
		HostedServices.Register<CoinJoinManager>(() => new CoinJoinManager(WalletManager, HostedServices.Get<RoundStateUpdater>(), wabiSabiHttpClientFactory, HostedServices.Get<WasabiSynchronizer>(), coinJoinConfiguration, CoinPrison), "CoinJoin Manager");
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

				if (_blockDownloadService is { } blockDownloadService)
				{
					blockDownloadService.Dispose();
					Logger.LogInfo($"{nameof(BlockDownloadService)} is disposed.");
				}

				if (_specificNodeBlockProvider is { } specificNodeBlockProvider)
				{
					await specificNodeBlockProvider.DisposeAsync().ConfigureAwait(false);
					Logger.LogInfo($"{nameof(SpecificNodeBlockProvider)} is disposed.");
				}

				if (HostedServices is { } backgroundServices)
				{
					using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(21));
					await backgroundServices.StopAllAsync(cts.Token).ConfigureAwait(false);
					backgroundServices.Dispose();
					Logger.LogInfo("Stopped background services.");
				}

				if (BitcoinCoreNode is { } bitcoinCoreNode)
				{
					await bitcoinCoreNode.DisposeAsync().ConfigureAwait(false);
					Logger.LogInfo($"{nameof(BitcoinCoreNode)} is disposed.");

					if (Config.StopLocalBitcoinCoreOnShutdown)
					{
						await bitcoinCoreNode.TryStopAsync().ConfigureAwait(false);
						Logger.LogInfo($"{nameof(BitcoinCoreNode)} is stopped.");
					}
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
