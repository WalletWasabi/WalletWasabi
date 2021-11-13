using Microsoft.Extensions.Caching.Memory;
using NBitcoin;
using Nito.AsyncEx;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore;
using WalletWasabi.BitcoinCore.Endpointing;
using WalletWasabi.BitcoinCore.Mempool;
using WalletWasabi.BitcoinCore.Monitoring;
using WalletWasabi.BitcoinP2p;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.CoinJoin.Client;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Rpc;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Services;
using WalletWasabi.Services.Terminate;
using WalletWasabi.Stores;
using WalletWasabi.Tor;
using WalletWasabi.Tor.Socks5.Pool.Circuits;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.BlockstreamInfo;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Fluent
{
	public class Global
	{
		public const string ThemeBackgroundBrushResourceKey = "ThemeBackgroundBrush";
		public const string ApplicationAccentForegroundBrushResourceKey = "ApplicationAccentForegroundBrush";

		public string DataDir { get; }
		public TorSettings TorSettings { get; }
		public BitcoinStore BitcoinStore { get; }

		/// <summary>
		/// HTTP client factory for communicating with the Wasabi backend.
		/// </summary>
		public HttpClientFactory BackendHttpClientFactory { get; }

		/// <summary>
		/// HTTP client factory for communicating with external third parties.
		/// </summary>
		public HttpClientFactory ExternalHttpClientFactory { get; }

		public LegalChecker LegalChecker { get; private set; }
		public Config Config { get; }
		public WasabiSynchronizer Synchronizer { get; private set; }
		public WalletManager WalletManager { get; }
		public TransactionBroadcaster TransactionBroadcaster { get; set; }
		public CoinJoinProcessor CoinJoinProcessor { get; set; }
		private TorProcessManager? TorManager { get; set; }
		public CoreNode BitcoinCoreNode { get; private set; }
		public HostedServices HostedServices { get; }

		public UiConfig UiConfig { get; }

		public Network Network => Config.Network;

		public MemoryCache Cache { get; private set; }

		public JsonRpcServer? RpcServer { get; private set; }

		public Global(string dataDir, Config config, UiConfig uiConfig, WalletManager walletManager)
		{
			using (BenchmarkLogger.Measure())
			{
				DataDir = dataDir;
				Config = config;
				UiConfig = uiConfig;
				TorSettings = new TorSettings(DataDir, distributionFolderPath: EnvironmentHelpers.GetFullBaseDirectory(), Config.TerminateTorOnExit, Environment.ProcessId);

				HostedServices = new HostedServices();
				WalletManager = walletManager;

				var networkWorkFolderPath = Path.Combine(DataDir, "BitcoinStore", Network.ToString());
				var transactionStore = new AllTransactionStore(networkWorkFolderPath, Network);
				var indexStore = new IndexStore(Path.Combine(networkWorkFolderPath, "IndexStore"), Network, new SmartHeaderChain());
				var mempoolService = new MempoolService();
				var blocks = new FileSystemBlockRepository(Path.Combine(networkWorkFolderPath, "Blocks"), Network);

				BitcoinStore = new BitcoinStore(indexStore, transactionStore, mempoolService, blocks);

				if (Config.UseTor)
				{
					BackendHttpClientFactory = new HttpClientFactory(TorSettings.SocksEndpoint, backendUriGetter: () => Config.GetCurrentBackendUri());
					ExternalHttpClientFactory = new HttpClientFactory(TorSettings.SocksEndpoint, backendUriGetter: null);
				}
				else
				{
					BackendHttpClientFactory = new HttpClientFactory(torEndPoint: null, backendUriGetter: () => Config.GetFallbackBackendUri());
					ExternalHttpClientFactory = new HttpClientFactory(torEndPoint: null, backendUriGetter: null);
				}

				Synchronizer = new WasabiSynchronizer(BitcoinStore, BackendHttpClientFactory);
				LegalChecker = new(DataDir);
				TransactionBroadcaster = new TransactionBroadcaster(Network, BitcoinStore, BackendHttpClientFactory, WalletManager);
			}
		}

		/// <remarks>Use this variable as a guard to prevent touching <see cref="StoppingCts"/> that might have already been disposed.</remarks>
		private volatile bool _disposeRequested;

		/// <summary>Lock that makes sure the application initialization and dispose methods do not run concurrently.</summary>
		private AsyncLock InitializationAsyncLock { get; } = new();

		/// <summary>Cancellation token to cancel <see cref="InitializeNoWalletAsync(TerminateService)"/> processing.</summary>
		private CancellationTokenSource StoppingCts { get; } = new();

		public async Task InitializeNoWalletAsync(TerminateService terminateService)
		{
			// StoppingCts may be disposed at this point, so do not forward the cancellation token here.
			using (await InitializationAsyncLock.LockAsync())
			{
				Logger.LogTrace("Initialization started.");

				if (_disposeRequested)
				{
					return;
				}

				CancellationToken cancel = StoppingCts.Token;

				try
				{
					Cache = new MemoryCache(new MemoryCacheOptions
					{
						SizeLimit = 1_000,
						ExpirationScanFrequency = TimeSpan.FromSeconds(30)
					});
					var bstoreInitTask = BitcoinStore.InitializeAsync(cancel);

					HostedServices.Register<UpdateChecker>(new UpdateChecker(TimeSpan.FromMinutes(7), Synchronizer), "Software Update Checker");

					await LegalChecker.InitializeAsync(HostedServices.Get<UpdateChecker>()).ConfigureAwait(false);
					cancel.ThrowIfCancellationRequested();

					await StartTorProcessManagerAsync(cancel).ConfigureAwait(false);

					try
					{
						await bstoreInitTask.ConfigureAwait(false);

						// Make sure that the height of the wallets will not be better than the current height of the filters.
						WalletManager.SetMaxBestHeight(BitcoinStore.IndexStore.SmartHeaderChain.TipHeight);
					}
					catch (Exception ex) when (ex is not OperationCanceledException)
					{
						// If our internal data structures in the Bitcoin Store gets corrupted, then it's better to rescan all the wallets.
						WalletManager.SetMaxBestHeight(SmartHeader.GetStartingHeader(Network).Height);
						throw;
					}

					HostedServices.Register<P2pNetwork>(new P2pNetwork(Network, Config.GetBitcoinP2pEndPoint(), Config.UseTor ? TorSettings.SocksEndpoint : null, Path.Combine(DataDir, "BitcoinP2pNetwork"), BitcoinStore), "Bitcoin P2P Network");

					await StartLocalBitcoinNodeAsync(cancel).ConfigureAwait(false);

					RegisterFeeRateProviders();
					RegisterCoinJoinComponents();

					SleepInhibitor? sleepInhibitor = await SleepInhibitor.CreateAsync(HostedServices.Get<CoinJoinManager>()).ConfigureAwait(false);

					if (sleepInhibitor is not null)
					{
						HostedServices.Register<SleepInhibitor>(sleepInhibitor, "Sleep Inhibitor");
					}
					else
					{
						Logger.LogInfo("Sleep Inhibitor is not available on this platform.");
					}
					await HostedServices.StartAllAsync(cancel).ConfigureAwait(false);

					var requestInterval = Network == Network.RegTest ? TimeSpan.FromSeconds(5) : TimeSpan.FromSeconds(30);
					int maxFiltSyncCount = Network == Network.Main ? 1000 : 10000; // On testnet, filters are empty, so it's faster to query them together

					Synchronizer.Start(requestInterval, maxFiltSyncCount);
					Logger.LogInfo("Start synchronizing filters...");

					TransactionBroadcaster.Initialize(HostedServices.Get<P2pNetwork>().Nodes, BitcoinCoreNode?.RpcClient);
					CoinJoinProcessor = new CoinJoinProcessor(Network, Synchronizer, WalletManager, BitcoinCoreNode?.RpcClient);

					await StartRpcServerAsync(terminateService, cancel).ConfigureAwait(false);

					var blockProvider = new CachedBlockProvider(
						new SmartBlockProvider(
							new P2pBlockProvider(HostedServices.Get<P2pNetwork>().Nodes, BitcoinCoreNode, BackendHttpClientFactory, Config.ServiceConfiguration, Network),
							Cache),
						BitcoinStore.BlockRepository);

					WalletManager.RegisterServices(BitcoinStore, Synchronizer, Config.ServiceConfiguration, HostedServices.Get<HybridFeeProvider>(), blockProvider);
				}
				finally
				{
					Logger.LogTrace("Initialization finished.");
				}
			}
		}

		private async Task StartRpcServerAsync(TerminateService terminateService, CancellationToken cancel)
		{
			var jsonRpcServerConfig = new JsonRpcServerConfiguration(Config);
			if (jsonRpcServerConfig.IsEnabled)
			{
				RpcServer = new JsonRpcServer(this, jsonRpcServerConfig, terminateService);
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

		private async Task StartTorProcessManagerAsync(CancellationToken cancel)
		{
			if (Config.UseTor && Network != Network.RegTest)
			{
				using (BenchmarkLogger.Measure(operationName: "TorProcessManager.Start"))
				{
					TorManager = new TorProcessManager(TorSettings);
					await TorManager.StartAsync(cancel).ConfigureAwait(false);
					Logger.LogInfo($"{nameof(TorProcessManager)} is initialized.");
				}

				Tor.Http.TorHttpClient torHttpClient = BackendHttpClientFactory.NewTorHttpClient(Mode.DefaultCircuit);
				HostedServices.Register<TorMonitor>(new TorMonitor(period: TimeSpan.FromSeconds(3), fallbackBackendUri: Config.GetFallbackBackendUri(), torHttpClient, TorManager), nameof(TorMonitor));
			}
		}

		private async Task StartLocalBitcoinNodeAsync(CancellationToken cancel)
		{
			try
			{
				if (Config.StartLocalBitcoinCoreOnStartup)
				{
					BitcoinCoreNode = await CoreNode
						.CreateAsync(
							new CoreNodeParams(
								Network,
								BitcoinStore.MempoolService,
								Config.LocalBitcoinCoreDataDir,
								tryRestart: false,
								tryDeleteDataDir: false,
								EndPointStrategy.Default(Network, EndPointType.P2p),
								EndPointStrategy.Default(Network, EndPointType.Rpc),
								txIndex: null,
								prune: null,
								mempoolReplacement: "fee,optin",
								userAgent: $"/WasabiClient:{Constants.ClientVersion}/",
								fallbackFee: null, // ToDo: Maybe we should have it, not only for tests?
								Cache),
							cancel)
						.ConfigureAwait(false);

					RegisterLocalNodeDependantComponents();
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
			}
		}

		[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Services are disposed by HostedServices class.")]
		private void RegisterLocalNodeDependantComponents()
		{
			HostedServices.Register<BlockNotifier>(new BlockNotifier(TimeSpan.FromSeconds(7), BitcoinCoreNode.RpcClient, BitcoinCoreNode.P2pNode), "Block Notifier");
			HostedServices.Register<RpcMonitor>(new RpcMonitor(TimeSpan.FromSeconds(7), BitcoinCoreNode.RpcClient), "RPC Monitor");
			HostedServices.Register<RpcFeeProvider>(new RpcFeeProvider(TimeSpan.FromMinutes(1), BitcoinCoreNode.RpcClient, HostedServices.Get<RpcMonitor>()), "RPC Fee Provider");
			HostedServices.Register<MempoolMirror>(new MempoolMirror(TimeSpan.FromSeconds(21), BitcoinCoreNode.RpcClient, BitcoinCoreNode.P2pNode), "Full Node Mempool Mirror");
		}

		[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Services are disposed by HostedServices class.")]
		private void RegisterFeeRateProviders()
		{
			HostedServices.Register<BlockstreamInfoFeeProvider>(new BlockstreamInfoFeeProvider(TimeSpan.FromMinutes(3), new(Network, ExternalHttpClientFactory)) { IsPaused = true }, "Blockstream.info Fee Provider");
			HostedServices.Register<ThirdPartyFeeProvider>(new ThirdPartyFeeProvider(TimeSpan.FromSeconds(1), Synchronizer, HostedServices.Get<BlockstreamInfoFeeProvider>()), "Third Party Fee Provider");
			HostedServices.Register<HybridFeeProvider>(new HybridFeeProvider(HostedServices.Get<ThirdPartyFeeProvider>(), HostedServices.GetOrDefault<RpcFeeProvider>()), "Hybrid Fee Provider");
		}

		[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Services are disposed by HostedServices class.")]
		private void RegisterCoinJoinComponents()
		{
			HostedServices.Register<RoundStateUpdater>(new RoundStateUpdater(TimeSpan.FromSeconds(5), new WabiSabiHttpApiClient(BackendHttpClientFactory.NewBackendHttpClient(Mode.SingleCircuitPerLifetime))), "Round info updater");
			HostedServices.Register<CoinJoinManager>(new CoinJoinManager(WalletManager, HostedServices.Get<RoundStateUpdater>(), BackendHttpClientFactory, Config.ServiceConfiguration), "CoinJoin Manager");
		}

		public async Task DisposeAsync()
		{
			// Dispose method may be called just once.
			if (!_disposeRequested)
			{
				_disposeRequested = true;
				StoppingCts.Cancel();
			}
			else
			{
				return;
			}

			using (await InitializationAsyncLock.LockAsync())
			{
				Logger.LogWarning("Process is exiting.", nameof(Global));

				try
				{
					try
					{
						using var dequeueCts = new CancellationTokenSource(TimeSpan.FromMinutes(6));
						await WalletManager.RemoveAndStopAllAsync(dequeueCts.Token).ConfigureAwait(false);
						Logger.LogInfo($"{nameof(WalletManager)} is stopped.", nameof(Global));
					}
					catch (Exception ex)
					{
						Logger.LogError($"Error during {nameof(WalletManager.RemoveAndStopAllAsync)}: {ex}");
					}

					if (RpcServer is { } rpcServer)
					{
						using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(21));
						await rpcServer.StopAsync(cts.Token).ConfigureAwait(false);
						Logger.LogInfo($"{nameof(RpcServer)} is stopped.", nameof(Global));
					}

					if (CoinJoinProcessor is { } coinJoinProcessor)
					{
						coinJoinProcessor.Dispose();
						Logger.LogInfo($"{nameof(CoinJoinProcessor)} is disposed.");
					}

					if (LegalChecker is { } legalChecker)
					{
						legalChecker.Dispose();
						Logger.LogInfo($"Disposed {nameof(LegalChecker)}.");
					}

					if (HostedServices is { } backgroundServices)
					{
						using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(21));
						await backgroundServices.StopAllAsync(cts.Token).ConfigureAwait(false);
						backgroundServices.Dispose();
						Logger.LogInfo("Stopped background services.");
					}

					if (Synchronizer is { } synchronizer)
					{
						await synchronizer.StopAsync().ConfigureAwait(false);
						Logger.LogInfo($"{nameof(Synchronizer)} is stopped.");
					}

					if (ExternalHttpClientFactory is { } externalHttpClientFactory)
					{
						externalHttpClientFactory.Dispose();
						Logger.LogInfo($"{nameof(ExternalHttpClientFactory)} is disposed.");
					}

					if (BackendHttpClientFactory is { } backendHttpClientFactory)
					{
						backendHttpClientFactory.Dispose();
						Logger.LogInfo($"{nameof(BackendHttpClientFactory)} is disposed.");
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

					if (TorManager is { } torManager)
					{
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
						await BitcoinStore.DisposeAsync().ConfigureAwait(false);
						Logger.LogInfo($"{nameof(BitcoinStore)} is disposed.");
					}
					catch (Exception ex)
					{
						Logger.LogError($"Error during the disposal of {nameof(BitcoinStore)}: {ex}");
					}
				}
				catch (Exception ex)
				{
					Logger.LogWarning(ex);
				}
				finally
				{
					StoppingCts.Dispose();
					Logger.LogTrace("Dispose finished.");
				}
			}
		}
	}
}
