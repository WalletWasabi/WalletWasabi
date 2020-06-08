using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Microsoft.Extensions.Caching.Memory;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using NBitcoin.Protocol.Connectors;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore;
using WalletWasabi.BitcoinCore.Endpointing;
using WalletWasabi.BitcoinCore.Monitoring;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.CoinJoin.Client;
using WalletWasabi.Gui.Container;
using WalletWasabi.Gui.Models;
using WalletWasabi.Gui.Rpc;
using WalletWasabi.Helpers;
using WalletWasabi.Legal;
using WalletWasabi.Logging;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.TorSocks5;
using WalletWasabi.Wallets;
using static WalletWasabi.Gui.Container.KillHandler;

namespace WalletWasabi.Gui
{
	public class Global : IProgramLifecycle
	{
		public const string ThemeBackgroundBrushResourceKey = "ThemeBackgroundBrush";
		public const string ApplicationAccentForegroundBrushResourceKey = "ApplicationAccentForegroundBrush";

		public string DataDir { get; }
		public string TorLogsFile { get; }
		public BitcoinStore BitcoinStore { get; }
		public LegalDocuments LegalDocuments { get; set; }
		public KillHandler KillHandler { get; }
		public Config Config { get; }

		public string AddressManagerFilePath { get; private set; }
		public AddressManager AddressManager { get; private set; }

		public NodesGroup Nodes { get; private set; }
		public WasabiSynchronizer Synchronizer { get; }
		public FeeProviders FeeProviders { get; private set; }
		public WalletManager WalletManager { get; }
		public WalletManagerLifecycle WalletManagerLifecycle { get; }
		public TransactionBroadcaster TransactionBroadcaster { get; set; }
		public CoinJoinProcessor CoinJoinProcessor { get; set; }
		public Node RegTestMempoolServingNode { get; private set; }
		public TorProcessManager TorManager { get; private set; }
		public CoreNode BitcoinCoreNode { get; private set; }

		public HostedServices HostedServices { get; }

		public UiConfig UiConfig { get; }

		public Network Network => Config.Network;

		public MemoryCache Cache { get; private set; }

		public static JsonRpcServer RpcServer { get; private set; }

		public Global(string dataDir, string torLogsFile, BitcoinStore bitcoinStore, 
			HostedServices hostedServices, UiConfig uiConfig, 
			WalletManager walletManager, WalletManagerLifecycle walletManagerLifecycle, 
			LegalDocuments legalDocuments, KillHandler killHandler, WasabiSynchronizer synchronizer,
			TorProcessManager torManager)
		{
			DataDir = dataDir;
			TorLogsFile = torLogsFile;
			BitcoinStore = bitcoinStore;
			HostedServices = hostedServices;
			UiConfig = uiConfig;
			WalletManager = walletManager;
			WalletManagerLifecycle = walletManagerLifecycle;
			LegalDocuments = legalDocuments;
			KillHandler = killHandler;
			Synchronizer = synchronizer;
			TorManager = torManager;
			StoppingCts = new CancellationTokenSource();
		}

		private bool InitializationCompleted { get; set; } = false;

		private bool InitializationStarted { get; set; } = false;

		private CancellationTokenSource StoppingCts { get; }

		public async Task InitializeNoWalletAsync()
		{
			InitializationStarted = true;
			AddressManager = null;
			TorManager = null;
			var cancel = StoppingCts.Token;

			try
			{
				var bstoreInitTask = BitcoinStore.InitializeAsync();
				var addressManagerFolderPath = Path.Combine(DataDir, "AddressManager");

				AddressManagerFilePath = Path.Combine(addressManagerFolderPath, $"AddressManager{Network}.dat");
				var addrManTask = InitializeAddressManagerBehaviorAsync();

				var blocksFolderPath = Path.Combine(DataDir, $"Blocks{Network}");
				var connectionParameters = new NodeConnectionParameters { UserAgent = "/Satoshi:0.18.1/" };

				HostedServices.Register(new UpdateChecker(TimeSpan.FromMinutes(7), Synchronizer), "Software Update Checker");

				Cache = new MemoryCache(new MemoryCacheOptions
				{
					SizeLimit = 1_000,
					ExpirationScanFrequency = TimeSpan.FromSeconds(30)
				});

				#region ProcessKillSubscription

				AppDomain.CurrentDomain.ProcessExit += async (s, e) => await DisposeAsync().ConfigureAwait(false);
				Console.CancelKeyPress += async (s, e) =>
				{
					e.Cancel = true;
					Logger.LogWarning("Process was signaled for killing.", nameof(Global));
					await DisposeAsync().ConfigureAwait(false);
				};

				#endregion ProcessKillSubscription

				cancel.ThrowIfCancellationRequested();

				#region TorProcessInitialization
				
				TorManager.Start(false);

				var fallbackRequestTestUri = new Uri(Config.GetFallbackBackendUri(), "/api/software/versions");
				TorManager.StartMonitor(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(7), fallbackRequestTestUri);

				Logger.LogInfo($"{nameof(TorProcessManager)} is initialized.");

				#endregion TorProcessInitialization

				cancel.ThrowIfCancellationRequested();

				#region BitcoinStoreInitialization

				await bstoreInitTask;

				// Make sure that the height of the wallets will not be better than the current height of the filters.
				WalletManager.SetMaxBestHeight(BitcoinStore.IndexStore.SmartHeaderChain.TipHeight);

				#endregion BitcoinStoreInitialization

				cancel.ThrowIfCancellationRequested();

				#region BitcoinCoreInitialization

				try
				{
					if (Config.StartLocalBitcoinCoreOnStartup)
					{
						BitcoinCoreNode = await CoreNode
							.CreateAsync(
								new CoreNodeParams(
									Network,
									BitcoinStore.MempoolService,
									HostedServices,
									Config.LocalBitcoinCoreDataDir,
									tryRestart: false,
									tryDeleteDataDir: false,
									EndPointStrategy.Default(Network, EndPointType.P2p),
									EndPointStrategy.Default(Network, EndPointType.Rpc),
									txIndex: null,
									prune: null,
									userAgent: $"/WasabiClient:{Constants.ClientVersion}/",
									Cache),
								cancel)
							.ConfigureAwait(false);
					}
				}
				catch (Exception ex)
				{
					Logger.LogError(ex);
				}

				await HostedServices.StartAllAsync(cancel).ConfigureAwait(false);

				var feeProviderList = new List<IFeeProvider>
				{
					Synchronizer
				};

				var rpcFeeProvider = HostedServices.FirstOrDefault<RpcFeeProvider>();
				if (rpcFeeProvider is { })
				{
					feeProviderList.Insert(0, rpcFeeProvider);
				}

				FeeProviders = new FeeProviders(feeProviderList);

				#endregion BitcoinCoreInitialization

				cancel.ThrowIfCancellationRequested();

				#region MempoolInitialization

				connectionParameters.TemplateBehaviors.Add(BitcoinStore.CreateUntrustedP2pBehavior());

				#endregion MempoolInitialization

				cancel.ThrowIfCancellationRequested();

				#region AddressManagerInitialization

				AddressManagerBehavior addressManagerBehavior = await addrManTask;
				connectionParameters.TemplateBehaviors.Add(addressManagerBehavior);

				#endregion AddressManagerInitialization

				cancel.ThrowIfCancellationRequested();

				#region P2PInitialization

				if (Network == Network.RegTest)
				{
					Nodes = new NodesGroup(Network, requirements: Constants.NodeRequirements);
					try
					{
						EndPoint bitcoinCoreEndpoint = Config.GetBitcoinP2pEndPoint();

						Node node = await Node.ConnectAsync(Network.RegTest, bitcoinCoreEndpoint).ConfigureAwait(false);

						Nodes.ConnectedNodes.Add(node);

						RegTestMempoolServingNode = await Node.ConnectAsync(Network.RegTest, bitcoinCoreEndpoint).ConfigureAwait(false);

						RegTestMempoolServingNode.Behaviors.Add(BitcoinStore.CreateUntrustedP2pBehavior());
					}
					catch (SocketException ex)
					{
						Logger.LogError(ex);
					}
				}
				else
				{
					if (Config.UseTor)
					{
						// onlyForOnionHosts: false - Connect to clearnet IPs through Tor, too.
						connectionParameters.TemplateBehaviors.Add(new SocksSettingsBehavior(Config.TorSocks5EndPoint, onlyForOnionHosts: false, networkCredential: null, streamIsolation: true));
						// allowOnlyTorEndpoints: true - Connect only to onions and do not connect to clearnet IPs at all.
						// This of course makes the first setting unnecessary, but it's better if that's around, in case someone wants to tinker here.
						connectionParameters.EndpointConnector = new DefaultEndpointConnector(allowOnlyTorEndpoints: Network == Network.Main);

						await AddKnownBitcoinFullNodeAsHiddenServiceAsync(AddressManager);
					}
					Nodes = new NodesGroup(Network, connectionParameters, requirements: Constants.NodeRequirements);
					Nodes.MaximumNodeConnection = 12;
					RegTestMempoolServingNode = null;
				}

				Nodes.Connect();
				Logger.LogInfo("Start connecting to nodes...");

				var regTestMempoolServingNode = RegTestMempoolServingNode;
				if (regTestMempoolServingNode is { })
				{
					regTestMempoolServingNode.VersionHandshake();
					Logger.LogInfo("Start connecting to mempool serving regtest node...");
				}

				#endregion P2PInitialization

				cancel.ThrowIfCancellationRequested();

				#region SynchronizerInitialization

				var requestInterval = TimeSpan.FromSeconds(30);
				if (Network == Network.RegTest)
				{
					requestInterval = TimeSpan.FromSeconds(5);
				}

				int maxFiltSyncCount = Network == Network.Main ? 1000 : 10000; // On testnet, filters are empty, so it's faster to query them together

				Synchronizer.Start(requestInterval, TimeSpan.FromMinutes(5), maxFiltSyncCount);
				Logger.LogInfo("Start synchronizing filters...");

				#endregion SynchronizerInitialization

				cancel.ThrowIfCancellationRequested();

				TransactionBroadcaster = new TransactionBroadcaster(Network, BitcoinStore, Synchronizer, Nodes, WalletManager, BitcoinCoreNode?.RpcClient);
				CoinJoinProcessor = new CoinJoinProcessor(Synchronizer, WalletManager, BitcoinCoreNode?.RpcClient);

				#region JsonRpcServerInitialization

				var jsonRpcServerConfig = new JsonRpcServerConfiguration(Config);
				if (jsonRpcServerConfig.IsEnabled)
				{
					RpcServer = new JsonRpcServer(this, jsonRpcServerConfig);
					try
					{
						await RpcServer.StartAsync(cancel).ConfigureAwait(false);
					}
					catch (System.Net.HttpListenerException e)
					{
						Logger.LogWarning($"Failed to start {nameof(JsonRpcServer)} with error: {e.Message}.");
						RpcServer = null;
					}
				}

				#endregion JsonRpcServerInitialization

				#region Blocks provider

				var blockProvider = new CachedBlockProvider(
					new SmartBlockProvider(
						new P2pBlockProvider(Nodes, BitcoinCoreNode, Synchronizer, Config.ServiceConfiguration, Network),
						Cache),
					new FileSystemBlockRepository(blocksFolderPath, Network));

				#endregion Blocks provider

				WalletManager.RegisterServices(BitcoinStore, Synchronizer, Nodes, Config.ServiceConfiguration, FeeProviders, blockProvider);
			}
			finally
			{
				InitializationCompleted = true;
			}
		}

		private async Task<AddressManagerBehavior> InitializeAddressManagerBehaviorAsync()
		{
			var needsToDiscoverPeers = true;
			if (Network == Network.RegTest)
			{
				AddressManager = new AddressManager();
				Logger.LogInfo($"Fake {nameof(AddressManager)} is initialized on the {Network.RegTest}.");
			}
			else
			{
				try
				{
					AddressManager = await NBitcoinHelpers.LoadAddressManagerFromPeerFileAsync(AddressManagerFilePath);

					// Most of the times we do not need to discover new peers. Instead, we can connect to
					// some of those that we already discovered in the past. In this case we assume that
					// discovering new peers could be necessary if our address manager has less
					// than 500 addresses. 500 addresses could be okay because previously we tried with
					// 200 and only one user reported he/she was not able to connect (there could be many others,
					// of course).
					// On the other side, increasing this number forces users that do not need to discover more peers
					// to spend resources (CPU/bandwidth) to discover new peers.
					needsToDiscoverPeers = Config.UseTor || AddressManager.Count < 500;
					Logger.LogInfo($"Loaded {nameof(AddressManager)} from `{AddressManagerFilePath}`.");
				}
				catch (DirectoryNotFoundException ex)
				{
					Logger.LogInfo($"{nameof(AddressManager)} did not exist at `{AddressManagerFilePath}`. Initializing new one.");
					Logger.LogTrace(ex);
					AddressManager = new AddressManager();
				}
				catch (FileNotFoundException ex)
				{
					Logger.LogInfo($"{nameof(AddressManager)} did not exist at `{AddressManagerFilePath}`. Initializing new one.");
					Logger.LogTrace(ex);
					AddressManager = new AddressManager();
				}
				catch (OverflowException ex)
				{
					// https://github.com/zkSNACKs/WalletWasabi/issues/712
					Logger.LogInfo($"{nameof(AddressManager)} has thrown `{nameof(OverflowException)}`. Attempting to autocorrect.");
					File.Delete(AddressManagerFilePath);
					Logger.LogTrace(ex);
					AddressManager = new AddressManager();
					Logger.LogInfo($"{nameof(AddressManager)} autocorrection is successful.");
				}
				catch (FormatException ex)
				{
					// https://github.com/zkSNACKs/WalletWasabi/issues/880
					Logger.LogInfo($"{nameof(AddressManager)} has thrown `{nameof(FormatException)}`. Attempting to autocorrect.");
					File.Delete(AddressManagerFilePath);
					Logger.LogTrace(ex);
					AddressManager = new AddressManager();
					Logger.LogInfo($"{nameof(AddressManager)} autocorrection is successful.");
				}
			}

			var addressManagerBehavior = new AddressManagerBehavior(AddressManager)
			{
				Mode = needsToDiscoverPeers ? AddressManagerBehaviorMode.Discover : AddressManagerBehaviorMode.None
			};
			return addressManagerBehavior;
		}

		private async Task AddKnownBitcoinFullNodeAsHiddenServiceAsync(AddressManager addressManager)
		{
			if (Network == Network.RegTest)
			{
				return;
			}

			// curl -s https://bitnodes.21.co/api/v1/snapshots/latest/ | egrep -o '[a-z0-9]{16}\.onion:?[0-9]*' | sort -ru
			// Then filtered to include only /Satoshi:0.17.x
			var fullBaseDirectory = EnvironmentHelpers.GetFullBaseDirectory();

			var onions = await File.ReadAllLinesAsync(Path.Combine(fullBaseDirectory, "OnionSeeds", $"{Network}OnionSeeds.txt"));

			onions.Shuffle();
			foreach (var onion in onions.Take(60))
			{
				if (EndPointParser.TryParse(onion, Network.DefaultPort, out var endpoint))
				{
					await addressManager.AddAsync(endpoint);
				}
			}
		}

		/// <returns>If initialization is successful, otherwise it was interrupted which means stopping was requested.</returns>
		public async Task<bool> WaitForInitializationCompletedAsync(CancellationToken cancellationToken)
		{
			while (!InitializationCompleted)
			{
				await Task.Delay(100, cancellationToken).ConfigureAwait(false);
			}

			return !StoppingCts.IsCancellationRequested;
		}


		public async Task DisposeAsync()
		{
			var compareRes = KillHandler.CompareExchange(Status.SOMEBODY_CALLED, Status.NOBODY_CALLED);

			if (compareRes == Status.SOMEBODY_CALLED)
			{
				while (KillHandler.GetState() != Status.CALL_FINISHED)
				{
					await Task.Delay(50);
				}
				return;
			}
			else if (compareRes == KillHandler.Status.CALL_FINISHED)
			{
				return;
			}

			Logger.LogWarning("Process is exiting.", nameof(Global));

			try
			{
				StoppingCts?.Cancel();

				if (!InitializationStarted)
				{
					return;
				}

				try
				{
					using var initCts = new CancellationTokenSource(TimeSpan.FromMinutes(6));
					await WaitForInitializationCompletedAsync(initCts.Token).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					Logger.LogError($"Error during {nameof(WaitForInitializationCompletedAsync)}: {ex}");
				}

				try
				{
					using var dequeueCts = new CancellationTokenSource(TimeSpan.FromMinutes(6));
					await WalletManager.RemoveAndStopAllAsync(dequeueCts.Token).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					Logger.LogError($"Error during {nameof(WalletManager.RemoveAndStopAllAsync)}: {ex}");
				}

				Dispatcher.UIThread.PostLogException(() =>
				{
					var window = (Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime).MainWindow;
					window?.Close();
				});

				WalletManagerLifecycle.OnDestroy();

				var rpcServer = RpcServer;
				if (rpcServer is { })
				{
					using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(21));
					await rpcServer.StopAsync(cts.Token);
					Logger.LogInfo($"{nameof(RpcServer)} is stopped.", nameof(Global));
				}

				var feeProviders = FeeProviders;
				if (feeProviders is { })
				{
					feeProviders.Dispose();
					Logger.LogInfo($"Disposed {nameof(FeeProviders)}.");
				}

				var coinJoinProcessor = CoinJoinProcessor;
				if (coinJoinProcessor is { })
				{
					coinJoinProcessor.Dispose();
					Logger.LogInfo($"{nameof(CoinJoinProcessor)} is disposed.");
				}

				var synchronizer = Synchronizer;
				if (synchronizer is { })
				{
					await synchronizer.StopAsync();
					Logger.LogInfo($"{nameof(Synchronizer)} is stopped.");
				}

				var backgroundServices = HostedServices;
				if (backgroundServices is { })
				{
					using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(21));
					await backgroundServices.StopAllAsync(cts.Token).ConfigureAwait(false);
					backgroundServices.Dispose();
					Logger.LogInfo("Stopped background services.");
				}

				var addressManagerFilePath = AddressManagerFilePath;
				if (addressManagerFilePath is { })
				{
					IoHelpers.EnsureContainingDirectoryExists(addressManagerFilePath);
					var addressManager = AddressManager;
					if (addressManager is { })
					{
						addressManager.SavePeerFile(AddressManagerFilePath, Config.Network);
						Logger.LogInfo($"{nameof(AddressManager)} is saved to `{AddressManagerFilePath}`.");
					}
				}

				var nodes = Nodes;
				if (nodes is { })
				{
					nodes.Disconnect();
					while (nodes.ConnectedNodes.Any(x => x.IsConnected))
					{
						await Task.Delay(50);
					}
					nodes.Dispose();
					Logger.LogInfo($"{nameof(Nodes)} are disposed.");
				}

				var regTestMempoolServingNode = RegTestMempoolServingNode;
				if (regTestMempoolServingNode is { })
				{
					regTestMempoolServingNode.Disconnect();
					Logger.LogInfo($"{nameof(RegTestMempoolServingNode)} is disposed.");
				}

				var bitcoinCoreNode = BitcoinCoreNode;
				if (bitcoinCoreNode is { })
				{
					await bitcoinCoreNode.DisposeAsync().ConfigureAwait(false);

					if (Config.StopLocalBitcoinCoreOnShutdown)
					{
						await bitcoinCoreNode.TryStopAsync().ConfigureAwait(false);
					}
				}

				var torManager = TorManager;
				if (torManager is { })
				{
					await torManager.StopAsync();
					Logger.LogInfo($"{nameof(TorManager)} is stopped.");
				}

				var cache = Cache;
				if (cache is { })
				{
					cache.Dispose();
				}

				if (AsyncMutex.IsAny)
				{
					try
					{
						await AsyncMutex.WaitForAllMutexToCloseAsync();
						Logger.LogInfo($"{nameof(AsyncMutex)}(es) are stopped.");
					}
					catch (Exception ex)
					{
						Logger.LogError($"Error during stopping {nameof(AsyncMutex)}: {ex}");
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}
			finally
			{
				StoppingCts?.Dispose();
				KillHandler.SetStatus(KillHandler.Status.CALL_FINISHED);

				Logger.LogSoftwareStopped("Wasabi");
			}
		}
	}
}
