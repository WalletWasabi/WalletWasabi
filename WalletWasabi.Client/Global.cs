using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.RPC;
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
using WalletWasabi.BitcoinP2p;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Client.Configuration;
using WalletWasabi.Client.Rpc;
using WalletWasabi.Discoverability;
using WalletWasabi.Extensions;
using WalletWasabi.FeeRateEstimation;
using WalletWasabi.Helpers;
using WalletWasabi.Io;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Observability;
using WalletWasabi.Rpc;
using WalletWasabi.Services;
using WalletWasabi.Services.NodesManagement;
using WalletWasabi.Services.Terminate;
using WalletWasabi.Stores;
using WalletWasabi.Tor;
using WalletWasabi.Tor.Control;
using WalletWasabi.Tor.StatusChecker;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.Banning;
using WalletWasabi.WabiSabi.Client.RoundStateAwaiters;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.Wallets;
using WalletWasabi.Wallets.Exchange;
using WalletWasabi.WebClients.Wasabi;
using static WalletWasabi.Services.Workers;
using ChainHeight = WalletWasabi.Models.Height.ChainHeight;

namespace WalletWasabi.Client;

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
		Status.DisposeUsing(_disposables);

		HostedServices = new HostedServices();
		HostedServices.DisposeUsing(_disposables);

		_mempoolService = new MempoolService(EventBus);
		FilterHeaders = new FilterHeaderChain();
		var networkWorkFolderPath = Path.Combine(DataDir, "BitcoinStore", Network.ToString());
		var fileSystemBlockRepository = new FileSystemBlockRepository(Path.Combine(networkWorkFolderPath, "Blocks"), Network);

		TransactionStore = new AllTransactionStore(networkWorkFolderPath, Network);
		TransactionStore.DisposeUsing(_disposables);

		FilterStore = new FilterStore(Path.Combine(networkWorkFolderPath, "IndexStore"), Network, FilterHeaders, EventBus);
		FilterStore.DisposeUsing(_disposables);

		ExternalSourcesHttpClientFactory = BuildHttpClientFactory();

		_metricManager = new MetricManager();
		_metricManager.DisposeUsing(_disposables);
		_metricDisplayService = new MetricDisplayService(_metricManager);
		_metricDisplayService.DisposeUsing(_disposables);

		var p2PDataDir = GetBitcoinP2PNetworkDirectory();
		_blockHeaders = ConfigureBlockHeaderChain(p2PDataDir);

		_nodeConnectionManager = ConfigureNodeConnectionManager();
		_bitcoinRpcClient = ConfigureBitcoinRpcClient();
		var cpfpProvider = ConfigureCpfpInfoProvider();
		var blockProvider = ConfigureBlockProvider(_nodeConnectionManager, fileSystemBlockRepository);

		var walletFactory = Wallet.CreateFactory(
			Config.Network,
			FilterStore,
			TransactionStore,
			FilterHeaders,
			_mempoolService,
			Config.ServiceConfiguration,
			blockProvider,
			EventBus,
			cpfpProvider);

		var walletDirectories = new WalletDirectories(Config.Network, DataDir);
		WalletManager = new WalletManager(Config.Network, walletDirectories, walletFactory);

		var broadcasters = CreateBroadcasters(_nodeConnectionManager, _mempoolService);
		TransactionBroadcaster = new TransactionBroadcaster(broadcasters.ToArray(), _mempoolService);

		Scheme = new Scheme(this);

		_ticker = new Timer(_ => EventBus.Publish(new Tick(DateTime.UtcNow)));
		_ticker.DisposeUsing(_disposables);

		_stoppingCts.DisposeUsing(_disposables);
	}

	private readonly AsyncLock _initializationAsyncLock = new();
	private readonly CancellationTokenSource _stoppingCts = new();
	private readonly MetricManager _metricManager;
	private readonly MetricDisplayService _metricDisplayService;
	private readonly NodeConnectionManager _nodeConnectionManager;
	private TorManager? _torManager;
	private readonly IRPCClient? _bitcoinRpcClient;
	private CoinPrison? _coinPrison;
	private readonly ConcurrentChain _blockHeaders;
	private readonly Timer _ticker;
	private readonly ComposedDisposable _disposables = new();
	private readonly MempoolService _mempoolService;
	private readonly ComposedAsyncDisposable _asyncDisposables = new();

	public StatusContainer Status { get; }
	public string DataDir { get; }
	public TorSettings TorSettings { get; }

	public FilterHeaderChain FilterHeaders { get; }
	public FilterStore FilterStore { get; }
	public AllTransactionStore TransactionStore { get; }
	public IHttpClientFactory ExternalSourcesHttpClientFactory { get; }
	public Config Config { get; }
	public WalletManager WalletManager { get; }
	public TransactionBroadcaster TransactionBroadcaster { get; }
	public HostedServices HostedServices { get; }
	public Network Network => Config.Network;
	public JsonRpcServer? RpcServer { get; private set; }
	public Uri? OnionServiceUri { get; private set; }
	public EventBus EventBus { get; }
	public Scheme Scheme { get; }

	private string GetBitcoinP2PNetworkDirectory() => Path.Combine(DataDir, "BitcoinP2pNetwork");

	private BlockProvider ConfigureBlockProvider(INodesRegistry nodesRegistry, FileSystemBlockRepository fileSystemBlockRepository)
	{
		var fileSystemBlockProvider = BlockProviders.FileSystemBlockProvider(fileSystemBlockRepository);
		var p2PBlockProvider = BlockProviders.P2pBlockProvider(nodesRegistry, EventBus);

		BlockProvider[] blockProviders = _bitcoinRpcClient is null
			? [fileSystemBlockProvider, p2PBlockProvider]
			: [fileSystemBlockProvider, BlockProviders.RpcBlockProvider(_bitcoinRpcClient), p2PBlockProvider];

		return BlockProviders.CachedBlockProvider(
			BlockProviders.ComposedBlockProvider(blockProviders),
			fileSystemBlockRepository);
	}

	private ConcurrentChain ConfigureBlockHeaderChain(string p2PDataDir)
	{
		var blockHeadersFilePath = Path.Combine(p2PDataDir, $"BlockHeaders{Network}.dat");
		var blockHeaders = Result<byte[], Exception>
			.Catch(() => File.SafelyReadAllBytes(blockHeadersFilePath))
			.Match(
				bytes => bytes switch
				{
					[] => new ConcurrentChain(Network),
					_ => new ConcurrentChain(bytes, Network)
				},
				_ => new ConcurrentChain(Network));

		return blockHeaders;
	}

	private NodeConnectionManager ConfigureNodeConnectionManager()
	{
		if (Network == Network.Main)
		{
			if (Network.DNSSeeds is List<DNSSeedData> dnsSeeds)
			{
				dnsSeeds.Add(new DNSSeedData("petertodd.net", "seed.btc.petertodd.net"));
				dnsSeeds.Add(new DNSSeedData("sprovoost.nl", "seed.bitcoin.sprovoost.nl"));
				dnsSeeds.Add(new DNSSeedData("emzy.de", "dnsseed.emzy.de"));
				dnsSeeds.Add(new DNSSeedData("wiz.biz", "seed.bitcoin.wiz.biz"));
				dnsSeeds.Add(new DNSSeedData("achownodes.xyz", "seed.mainnet.achownodes.xyz"));
			}
		}
		if (Network == Bitcoin.Instance.Signet)
		{
			if (Network.DNSSeeds is List<DNSSeedData> dnsSeeds)
			{
				dnsSeeds.Add(new DNSSeedData("sprovoost.nl", "seed.signet.bitcoin.sprovoost.nl"));
				dnsSeeds.Add(new DNSSeedData("achownodes.xyz", "seed.signet.achownodes.xyz"));
			}
		}
		if (Network == Network.RegTest)
		{
			if (Network.SeedNodes is List<NetworkAddress> addresses)
			{
				addresses.Add(new NetworkAddress(IPAddress.Loopback, Network.DefaultPort));
			}
		}

		if (Network == Network.Main)
		{
			var extras = new[]
			{
				"[::ffff:89.58.60.208]:8333", "141.8.29.139:8333", "176.126.73.74:8333", "51.37.212.201:8333",
				"[::ffff:109.224.84.149]:8333", "[::ffff:123.202.192.214]:8333", "[::ffff:130.180.58.210]:8333",
				"[::ffff:144.2.65.179]:8333", "[::ffff:158.174.102.68]:8333", "[::ffff:158.248.16.134]:8333",
				"[::ffff:176.198.90.204]:8333", "[::ffff:176.9.150.253]:8333", "[::ffff:178.192.9.193]:8333",
				"[::ffff:178.26.46.97]:8333", "[::ffff:184.181.44.154]:8333", "[::ffff:217.92.137.136]:8333",
				"[::ffff:218.146.42.163]:8333", "[::ffff:24.134.6.165]:8333", "[::ffff:45.130.58.202]:8333",
				"[::ffff:45.41.51.43]:8333", "[::ffff:46.226.18.135]:8333", "[::ffff:46.229.238.187]:8333",
				"[::ffff:46.59.13.35]:8333", "[::ffff:47.181.150.194]:8333", "[::ffff:5.161.244.95]:8333",
				"[::ffff:62.177.111.78]:8333", "[::ffff:64.130.52.77]:8333", "[::ffff:71.185.186.173]:8333",
				"[::ffff:80.253.94.252]:8333", "[::ffff:82.67.127.46]:8333", "[::ffff:84.196.182.49]:8333",
				"[::ffff:86.242.20.49]:8333", "[::ffff:86.254.150.8]:8333", "[::ffff:87.236.195.198]:8333",
				"[::ffff:95.90.138.145]:8333", "179.51.86.34:8333", "188.63.47.92:8333",
				"54.248.26.73:8333", "82.67.161.60:8333", "92.252.69.140:8333", "92.98.3.189:8333",
				"[::ffff:109.202.209.123]:8333", "[::ffff:121.99.109.132]:8333", "[::ffff:13.48.242.195]:8333",
				"[::ffff:137.175.247.16]:8333", "[::ffff:141.195.182.138]:8333", "[::ffff:141.224.209.201]:8333",
				"[::ffff:15.134.155.244]:8333", "[::ffff:15.222.135.69]:8333", "[::ffff:159.196.59.9]:8333",
				"[::ffff:167.235.98.250]:8333", "[::ffff:174.165.47.165]:8333", "[::ffff:176.34.50.242]:8333",
				"[::ffff:178.26.219.209]:8333", "[::ffff:18.102.201.234]:8333", "[::ffff:181.115.88.2]:8333",
				"[::ffff:188.34.193.226]:8333", "[::ffff:194.160.169.63]:8333", "[::ffff:194.42.111.175]:8333",
				"[::ffff:203.12.2.113]:8333", "[::ffff:213.144.146.33]:8333", "[::ffff:3.23.202.194]:8333",
				"[::ffff:3.24.243.206]:8333", "[::ffff:38.172.231.13]:8333", "[::ffff:43.200.189.153]:8333",
				"[::ffff:43.202.209.174]:8333", "[::ffff:45.142.17.140]:8333", "[::ffff:47.130.216.204]:8333",
				"[::ffff:5.56.248.2]:8333", "[::ffff:50.106.24.94]:8333", "[::ffff:51.158.54.195]:8333",
				"[::ffff:51.44.200.138]:8333", "[::ffff:52.74.41.162]:8333", "[::ffff:54.246.85.218]:8333",
				"[::ffff:56.125.205.1]:8333", "[::ffff:56.125.249.13]:8333", "[::ffff:56.126.1.10]:8333",
				"[::ffff:56.126.33.251]:8333", "[::ffff:65.109.125.160]:8333", "[::ffff:68.103.11.30]:8333",
				"[::ffff:68.231.1.158]:8333", "[::ffff:71.179.175.122]:8333", "[::ffff:72.210.34.27]:8333",
				"[::ffff:72.253.193.231]:8333", "[::ffff:76.31.239.16]:8333", "[::ffff:77.162.78.194]:8333",
				"[::ffff:79.160.240.207]:8333", "[::ffff:82.66.107.156]:8333", "[::ffff:85.0.91.69]:8333",
				"[::ffff:85.3.52.172]:8333", "[::ffff:88.153.65.113]:8333", "[::ffff:89.217.193.252]:8333",
				"[::ffff:89.56.142.128]:8333", "[::ffff:89.56.206.21]:8333", "[::ffff:90.11.72.52]:8333",
				"[::ffff:90.189.215.153]:8333", "[::ffff:92.148.116.20]:8333", "[::ffff:93.56.5.69]:8333",
				"[::ffff:93.89.130.246]:8333"
			};

			if (Network.SeedNodes is List<NetworkAddress> addresses)
			{
				addresses.AddRange(extras.Select(IPEndPoint.Parse).Select(x => new NetworkAddress(x)));
			}
		}

		var torEndpoint = Config.UseTor != TorMode.Disabled ? TorSettings.SocksEndpoint : null;
		IDnsResolver dnsResolver = torEndpoint is not null
			? new DnsSocksResolver(torEndpoint)
			: DnsResolver.Instance;

		var manager = new NodeConnectionManager(
			Network,
			EventBus,
			dnsResolver,
			TimeSpan.FromSeconds(15),
			torSocks5: torEndpoint);

		if (!Config.BlockOnlyMode)
		{
			manager.AddBehavior(new BlockHeadersChainBehavior(_blockHeaders, FilterHeaders, EventBus));
			manager.AddBehavior(new P2pBehavior(_mempoolService));
		}

		manager.DisposeUsing(_disposables);
		return manager;
	}

	private void ConfigureBitcoinNetwork(CancellationToken cancellationToken)
	{
		_nodeConnectionManager.Start(cancellationToken);
	}

	private RpcClientBase? ConfigureBitcoinRpcClient()
	{
		var credentialString = Config.BitcoinRpcCredentialString;
		RPCCredentialString? credentials;

		if (string.IsNullOrWhiteSpace(credentialString))
		{
			credentials = new RPCCredentialString();
		}
		else if (!RPCCredentialString.TryParse(credentialString, out credentials))
		{
			return null;
		}

		if (string.IsNullOrWhiteSpace(Config.BitcoinRpcUri))
		{
			return null;
		}

		var bitcoinRpcUri = Config.BitcoinRpcUri;
		RPCClient internalRpcClient;

		try
		{
			internalRpcClient = new RPCClient(credentials, bitcoinRpcUri, Network);
		}
		catch (ArgumentException)
		{
			return null;
		}

		// Use Tor only if the address ends with .onion. Especially, do not use Tor for loopback (i.e. `localhost`).
		if (new Uri(bitcoinRpcUri).DnsSafeHost.EndsWith(".onion", StringComparison.OrdinalIgnoreCase))
		{
			internalRpcClient.HttpClient = ExternalSourcesHttpClientFactory.CreateClient("long-live-rpc-connection");
		}

		return new RpcClientBase(internalRpcClient);
	}

	private HttpClientFactory BuildHttpClientFactory(HttpClientHandlerConfiguration? config = null) =>
		Config.UseTor != TorMode.Disabled
			? new OnionHttpClientFactory(TorSettings.SocksEndpoint.ToUri("socks5"), config)
			: new HttpClientFactory(config);

	private void ConfigureFeeRateUpdater(CancellationToken cancellationToken)
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

		feeRateProvider = _bitcoinRpcClient is not null
			? FeeRateProviders.Composed([FeeRateProviders.RpcAsync(_bitcoinRpcClient), feeRateProvider])
			: feeRateProvider;
		var feeRateUpdater = Spawn("FeeRateUpdater",
			Service("Mining Fee Rate Updater",
				Periodically(
					TimeSpan.FromMinutes(15),
					FeeRateEstimations.Empty,
					FeeRateEstimationUpdater.CreateUpdater(feeRateProvider, EventBus))), cancellationToken);
		feeRateUpdater.DisposeUsing(_disposables);
		EventBus.Subscribe<Tick>(_ => feeRateUpdater.Post(new FeeRateEstimationUpdater.UpdateMessage()))
			.DisposeUsing(_disposables);
	}

	private void ConfigureRpcMonitor(CancellationToken cancellationToken)
	{
		if (_bitcoinRpcClient is null)
		{
			return;
		}
		var rpcMonitor = Spawn(RpcMonitor.ServiceName,
			Service("Bitcoin Rpc Interface Monitoring",
				Periodically(
					TimeSpan.FromSeconds(7),
					Unit.Instance,
					RpcMonitor.CreateChecker(_bitcoinRpcClient, EventBus))), cancellationToken);
		rpcMonitor.DisposeUsing(_disposables);
		EventBus.Subscribe<Tick>(_ => rpcMonitor.Post(new RpcMonitor.CheckMessage()))
			.DisposeUsing(_disposables);
	}

	private async Task ConfigureSynchronizerAsync(CancellationToken cancellationToken)
	{
		var supportsBlockFiltersResult = await (_bitcoinRpcClient is { } rpcClient
				? rpcClient.SupportsBlockFiltersAsync(cancellationToken)
				: Task.FromResult(Result<bool>.Fail(false)))
			.ConfigureAwait(false);

		var filtersProvider = supportsBlockFiltersResult
			.Map(_ => FilterProviders.CreateBitcoinRpcFilterProvider(_bitcoinRpcClient!, _blockHeaders))
			.Match(
				rpcProvider => rpcProvider,
				_ =>
				{
					var tip = FilterStore.GetTip()!.Header;
					var synchronizationState = new CompactFilterBehavior.FilterSynchronizationState(_blockHeaders, FilterHeaders, tip.Height);
					_nodeConnectionManager.AddBehavior(new CompactFilterBehavior(synchronizationState, _blockHeaders, EventBus));

					return FilterProviders.CreateBitcoinP2pFilterProvider(FilterHeaders, _blockHeaders, synchronizationState);
				});


		var (pause, resume, serviceLoop) =
			Continuously(Synchronizer.CreateFilterGenerator(filtersProvider, FilterStore, FilterHeaders, EventBus));

		if (supportsBlockFiltersResult.IsOk)
		{
			EventBus.Subscribe<RpcStatusChanged>(e =>
			{
				var action = e.Status.Match(
					x => x.Synchronized ? resume : pause,
					_ => pause);
				action();
			}).DisposeUsing(_disposables);
		}
		else
		{
			var errorBecauseIndexIsDisabled = supportsBlockFiltersResult.Error;
			if( errorBecauseIndexIsDisabled)
			{
				Logger.LogInfo("\nWasabi is connected to a bitcoin RPC that doesn't provides compact filters (BIP158)."
								+ "\nCompact filters are disabled by default in Bitcoin and you have to enable them."
								+ "\nIf you are using your own node then edit the bitcoin.conf file and add the line:"
								+ "\nblockfilterindex=1"
								+ "\n"
								+ "\nIf you are connected to a personal server product, some of them allow the user to enable"
								+ "\nthe block filters (BIP158) in the UI while others require you to edit the bitcoin config"
								+ "\nfile manually."
								+ "\n"
								+ "\nRemember to restart your bitcoin node after changing the configuration and wait for"
								+ "\nwait for it to create the filters, what can take some time."
								+ "\n-----------------------------------------------------------------------------------------");
			}
			else if(!string.IsNullOrWhiteSpace(Config.BitcoinRpcUri))
			{
				Logger.LogWarning($"Was not able to connect to the Bitcoin RPC server '{Config.BitcoinRpcUri}' with the credentials provided. " +
				                  "Please configure valid RPC credentials in settings and restart.");
			}
			else
			{
				Logger.LogInfo("No Bitcoin Node RPC was configured. Trying P2P synchronization.");
			}

			await resume().ConfigureAwait(false);
		}

		Spawn("Synchronizer", Service("Wasabi Index-Based Synchronizer", serviceLoop), cancellationToken)
			.DisposeUsing(_disposables);

		EventBus.Subscribe<RpcStatusChanged>(e =>
		{
			if (e.Status.IsOk)
			{
				var serverTip = (uint)e.Status.Value.Headers;
				FilterHeaders.SetServerTipHeight(new ChainHeight(serverTip));
				EventBus.Publish(new NetworkTipHeightChanged(serverTip));
			}
		}).DisposeUsing(_disposables);
	}

	private void ConfigureExchangeRateUpdater(CancellationToken cancellationToken)
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
						ExchangeRateUpdater.CreateExchangeRateUpdater(exchangeRateProvider, EventBus))), cancellationToken);
		exchangeFeeRateUpdater.DisposeUsing(_disposables);
		EventBus.Subscribe<Tick>(_ => exchangeFeeRateUpdater.Post(new ExchangeRateUpdater.UpdateMessage()))
			.DisposeUsing(_disposables);
	}

	private void ConfigureWasabiUpdater(CancellationToken cancellationToken)
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
					UpdateManager.CreateUpdater(nostrClientFactory, installerDownloader, EventBus))), cancellationToken);
		wasabiVersionUpdater.DisposeUsing(_disposables);
		EventBus.Subscribe<Tick>(_ => wasabiVersionUpdater.Post(new UpdateManager.UpdateMessage()))
			.DisposeUsing(_disposables);
	}

	private CpfpInfoProvider ConfigureCpfpInfoProvider()
	{
		var cpfpUpdater = Spawn("CpfpInfoProvider",
			Service("External Cpfp Info provider",
				EventDriven(
					Unit.Instance,
					Network == Network.RegTest
					? CpfpInfoUpdater.CreateForRegTest()
					: CpfpInfoUpdater.Create(ExternalSourcesHttpClientFactory, Network, EventBus))),
			_stoppingCts.Token);
		cpfpUpdater.DisposeUsing(_disposables);
		EventBus.Subscribe<FilterProcessed>(_ => cpfpUpdater.Post(new CpfpInfoMessage.UpdateMessage()))
			.DisposeUsing(_disposables);
		return new CpfpInfoProvider(cpfpUpdater);
	}

	private async Task InitializeBitcoinStoreAsync(CancellationToken cancellationToken)
	{
		try
		{
			await TransactionStore.InitializeAsync(cancellationToken).ConfigureAwait(false);
			await FilterStore.InitializeAsync(CalculateSafestHeight(), cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			Logger.LogError($"Bitcoin storage got corrupted. Resetting wallet(s) to the first block to rescan. Exception: {ex}");
			WalletManager.SetMaxBestHeight(CalculateSafestHeight());
			throw;
		}
	}

	private ChainHeight CalculateSafestHeight()
	{
		var checkpointHeight = FilterCheckpoints.GetMostRecentCheckpoint(Network).Header.Height;
		var transactionHeight = TransactionStore.TryGetOldestKnownTransactionHeight(out var h) ? h - Constants.ResyncHeightMargin : checkpointHeight;
		var birthHeight = WalletManager.GetEarliestBirthHeight();
		var worstBestHeight = WalletManager.GetWorstBestHeight();
		return (ChainHeight) Height.Min(checkpointHeight, ((ChainHeight?[]) [transactionHeight, birthHeight, worstBestHeight]).DropNulls());
	}

	public async Task InitializeAsync(bool initializeSleepInhibitor, TerminateService terminateService, CancellationToken cancellationToken)
	{
		using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stoppingCts.Token);
		CancellationToken linkedCtsToken = linkedCts.Token;

		await _metricDisplayService.StartAsync(linkedCtsToken).ConfigureAwait(false);
		ConfigureBitcoinNetwork(linkedCtsToken);
		ConfigureWasabiUpdater(linkedCtsToken);
		ConfigureExchangeRateUpdater(linkedCtsToken);
		ConfigureRpcMonitor(linkedCtsToken);
		ConfigureFeeRateUpdater(linkedCtsToken);

		// _stoppingCts may be disposed at this point, so do not forward the cancellation token here.
		using (await _initializationAsyncLock.LockAsync(linkedCtsToken))
		{
			Logger.LogTrace("Initialization started.");

			await Task.WhenAll(
				StartTorProcessManagerAsync(linkedCtsToken),
				InitializeBitcoinStoreAsync(linkedCtsToken))
				.ConfigureAwait(false);

			await ConfigureSynchronizerAsync(linkedCtsToken).ConfigureAwait(false);

			if (_disposeRequested)
			{
				return;
			}

			try
			{
				if (Config.TryGetCoordinatorUri(out var coordinatorUri))
				{
					RegisterCoinJoinComponents(coordinatorUri);

					if (initializeSleepInhibitor)
					{
						await CreateSleepInhibitorAsync().ConfigureAwait(false);
					}
				}

				await HostedServices.StartAllAsync(linkedCtsToken).ConfigureAwait(false);

				await StartRpcServerAsync(terminateService, linkedCtsToken).ConfigureAwait(false);
			}
			finally
			{
				Logger.LogTrace("Initialization finished.");
			}
			_ticker.Change(TimeSpan.Zero, TimeSpan.FromSeconds(1));
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
			var wasabiJsonRpcService = new WasabiJsonRpcService(global: this);
			RpcServer = new JsonRpcServer(wasabiJsonRpcService, jsonRpcServerConfig, terminateService);
			RpcServer.DisposeUsing(_disposables);
			try
			{
				await RpcServer.StartAsync(cancel).ConfigureAwait(false);
				RpcServer.DisposeUsing(_disposables);
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
			TorProcessManager processManager = new(TorSettings, EventBus);
			_torManager = new TorManager(TorSettings, processManager);
			_torManager.DisposeUsing(_asyncDisposables);
			await _torManager.StartAsync(attempts: 3, cancellationToken).ConfigureAwait(false);
			Logger.LogInfo($"{nameof(TorManager)} is initialized.");

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
					TorStatusChecker.CreateChecker(torStatusHttpClient, EventBus)),
				_stoppingCts.Token);
			torStatusChecker.DisposeUsing(_disposables);
			EventBus.Subscribe<Tick>(_ => torStatusChecker.Post(new TorStatusChecker.CheckMessage()))
				.DisposeUsing(_disposables);
		}
	}

	private void RegisterCoinJoinComponents(Uri coordinatorUri)
	{
		var prisonForCoordinator = Path.Combine(DataDir, coordinatorUri.Host);
		_coinPrison = CoinPrison.CreateOrLoadFromFile(prisonForCoordinator);
		_coinPrison.DisposeUsing(_disposables);

		EventBus
			.Subscribe<WalletLoaded>(e => _coinPrison.UpdateWallet(e.Wallet))
			.DisposeUsing(_disposables);

		// Aggressively retry
		var coordinatorHttpClientConfig = new HttpClientHandlerConfiguration
		{
			MaxAttempts = 10,
			TimeBeforeRetryingAfterNetworkError = TimeSpan.FromSeconds(0.5),
			TimeBeforeRetryingAfterServerError = TimeSpan.FromSeconds(0.5),
			TimeBeforeRetryingAfterTooManyRequests = TimeSpan.FromSeconds(0.1)
		};
		var coordinatorHttpClientFactory = new CoordinatorHttpClientFactory(coordinatorUri, BuildHttpClientFactory(coordinatorHttpClientConfig));

		var wabiSabiStatusProvider =  new WabiSabiHttpApiClient("satoshi-coordination", coordinatorHttpClientFactory);
		var roundUpdater = Spawn("RoundUpdater",
			Service("WabiSabi Rounds Updater",
				EventDriven(
					new RoundsState(DateTime.UtcNow, RoundStateProvider.QueryFrequency, new Dictionary<uint256, RoundState>(), []),
					RoundStateUpdater.Create(wabiSabiStatusProvider))),
			_stoppingCts.Token);
		roundUpdater.DisposeUsing(_disposables);
		EventBus.Subscribe<Tick>(_ => roundUpdater.Post(new RoundUpdateMessage.UpdateMessage(DateTime.UtcNow)))
			.DisposeUsing(_disposables);

		Func<string, WabiSabiHttpApiClient> wabiSabiHttpClientFactory = (identity) => new WabiSabiHttpApiClient(identity, coordinatorHttpClientFactory);
		var coinJoinConfiguration = new CoinJoinConfiguration(Config.CoordinatorIdentifier, Config.MaxCoinjoinMiningFeeRate, Config.AbsoluteMinInputCount, AllowSoloCoinjoining: false);
		HostedServices.Register<CoinJoinManager>(() => new CoinJoinManager(WalletManager.GetWalletsAsync, new RoundStateProvider(roundUpdater), wabiSabiHttpClientFactory, coinJoinConfiguration, _coinPrison, EventBus), "CoinJoin Manager");
	}

	private List<IBroadcaster> CreateBroadcasters(INodesRegistry nodes, MempoolService mempoolService)
	{
		List<IBroadcaster> result =
		[
			new NetworkBroadcaster(mempoolService, nodes)
		];

		if (_bitcoinRpcClient is not null)
		{
			result.Insert(0, new RpcBroadcaster(_bitcoinRpcClient));
		}

		if (Network != Network.RegTest)
		{
			var external = ExternalTransactionBroadcaster.GetSortedBroadcasters(Config.ExternalTransactionBroadcaster, Network)
				.Select(info => new ExternalTransactionBroadcaster(info, ExternalSourcesHttpClientFactory));
			result.AddRange(external);
		}

		return result;
	}

	public ImmutableArray<Node> GetNodes() => _nodeConnectionManager.Nodes;

	public async Task DisposeAsync()
	{
		// Dispose method may be called just once.
		if (!_disposeRequested)
		{
			_disposeRequested = true;
			await _stoppingCts.CancelAsync().ConfigureAwait(false);
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

				if (_blockHeaders.Tip is not null)
				{
					var p2PDataDir = GetBitcoinP2PNetworkDirectory();
					var blockHeadersFilePath = Path.Combine(p2PDataDir, $"BlockHeaders{Network}.dat");
					File.SafelyWriteAllBytes(blockHeadersFilePath, _blockHeaders.ToBytes());
					Logger.LogInfo("Block headers saved.");
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
					Logger.LogInfo("Stopped background services.");
				}

				if (_torManager is not null)
				{
					using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));

					var torControlClient =
						Result<(CancellationToken, TorControlClient), Exception>
						.Catch(async () => await _torManager.WaitForNextAttemptAsync(cts.Token).ConfigureAwait(false))
						.Map(x => x.Result.Item2)
						.AsNullable();

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

					Logger.LogInfo("TorManager is stopped.");
				}

				_disposables.Dispose();
				await _asyncDisposables.DisposeAsync().ConfigureAwait(false);

				Logger.LogInfo("Dispose meter listener.");
				_metricManager.Dispose();
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}
			finally
			{
				Logger.LogTrace("Dispose finished.");
			}
		}
	}
}
