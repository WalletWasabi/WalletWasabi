using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
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
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.CoinJoin.Client;
using WalletWasabi.CoinJoin.Client.Clients;
using WalletWasabi.CoinJoin.Client.Clients.Queuing;
using WalletWasabi.Gui.CrashReport;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Gui.Models;
using WalletWasabi.Gui.Rpc;
using WalletWasabi.Helpers;
using WalletWasabi.Hwi.Models;
using WalletWasabi.Legal;
using WalletWasabi.Logging;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.TorSocks5;
using WalletWasabi.Wallets;

namespace WalletWasabi.Gui
{
	public class Global
	{
		public const string ThemeBackgroundBrushResourceKey = "ThemeBackgroundBrush";
		public const string ApplicationAccentForegroundBrushResourceKey = "ApplicationAccentForegroundBrush";

		public string DataDir { get; }
		public string TorLogsFile { get; }
		public BitcoinStore BitcoinStore { get; }
		public LegalDocuments LegalDocuments { get; set; }
		public Config Config { get; }

		public string AddressManagerFilePath { get; private set; }
		public AddressManager AddressManager { get; private set; }

		public NodesGroup Nodes { get; private set; }
		public WasabiSynchronizer Synchronizer { get; private set; }
		public FeeProviders FeeProviders { get; private set; }
		public WalletManager WalletManager { get; }
		public TransactionBroadcaster TransactionBroadcaster { get; set; }
		public CoinJoinProcessor CoinJoinProcessor { get; set; }
		public Node RegTestMempoolServingNode { get; private set; }
		public TorProcessManager TorManager { get; private set; }
		public CoreNode BitcoinCoreNode { get; private set; }

		public HostedServices HostedServices { get; }

		public bool KillRequested => Interlocked.Read(ref _dispose) > 0;

		public UiConfig UiConfig { get; }

		public Network Network => Config.Network;

		public MemoryCache Cache { get; private set; }

		public static JsonRpcServer RpcServer { get; private set; }

		public Global(string dataDir, string torLogsFile, Config config, UiConfig uiConfig, WalletManager walletManager)
		{
			using (BenchmarkLogger.Measure())
			{
				CrashReporter = new CrashReporter();
				StoppingCts = new CancellationTokenSource();
				DataDir = dataDir;
				Config = config;
				UiConfig = uiConfig;
				TorLogsFile = torLogsFile;

				Logger.InitializeDefaults(Path.Combine(DataDir, "Logs.txt"));

				HostedServices = new HostedServices();
				WalletManager = walletManager;

				LegalDocuments = LegalDocuments.TryLoadAgreed(DataDir);

				WalletManager.OnDequeue += WalletManager_OnDequeue;
				WalletManager.WalletRelevantTransactionProcessed += WalletManager_WalletRelevantTransactionProcessed;

				var networkWorkFolderPath = Path.Combine(DataDir, "BitcoinStore", Network.ToString());
				var transactionStore = new AllTransactionStore(networkWorkFolderPath, Network);
				var indexStore = new IndexStore(Path.Combine(networkWorkFolderPath, "IndexStore"), Network, new SmartHeaderChain());
				var mempoolService = new MempoolService();
				var blocks = new FileSystemBlockRepository(Path.Combine(networkWorkFolderPath, "Blocks"), Network);

				BitcoinStore = new BitcoinStore(indexStore, transactionStore, mempoolService, blocks);

				SingleInstanceChecker = new SingleInstanceChecker(Network);
			}
		}

		private bool InitializationCompleted { get; set; } = false;

		private bool InitializationStarted { get; set; } = false;

		private CancellationTokenSource StoppingCts { get; }

		private SingleInstanceChecker SingleInstanceChecker { get; }
		public CrashReporter CrashReporter { get; }

		public async Task InitializeNoWalletAsync()
		{
			InitializationStarted = true;
			AddressManager = null;
			TorManager = null;
			var cancel = StoppingCts.Token;

			try
			{
				await SingleInstanceChecker.CheckAsync().ConfigureAwait(false);

				Cache = new MemoryCache(new MemoryCacheOptions
				{
					SizeLimit = 1_000,
					ExpirationScanFrequency = TimeSpan.FromSeconds(30)
				});
				var bstoreInitTask = BitcoinStore.InitializeAsync();
				var addressManagerFolderPath = Path.Combine(DataDir, "AddressManager");

				AddressManagerFilePath = Path.Combine(addressManagerFolderPath, $"AddressManager{Network}.dat");
				var addrManTask = InitializeAddressManagerBehaviorAsync();

				var userAgent = Constants.UserAgents.RandomElement();
				var connectionParameters = new NodeConnectionParameters { UserAgent = userAgent };

				if (Config.UseTor)
				{
					Synchronizer = new WasabiSynchronizer(Network, BitcoinStore, () => Config.GetCurrentBackendUri(), Config.TorSocks5EndPoint);
				}
				else
				{
					Synchronizer = new WasabiSynchronizer(Network, BitcoinStore, Config.GetFallbackBackendUri(), null);
				}

				HostedServices.Register(new UpdateChecker(TimeSpan.FromMinutes(7), Synchronizer), "Software Update Checker");

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

				if (Config.UseTor)
				{
					TorManager = new TorProcessManager(Config.TorSocks5EndPoint, TorLogsFile);
				}
				else
				{
					TorManager = TorProcessManager.Mock();
				}
				TorManager.Start(false, DataDir);

				var fallbackRequestTestUri = new Uri(Config.GetFallbackBackendUri(), "/api/software/versions");
				TorManager.StartMonitor(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(7), DataDir, fallbackRequestTestUri);

				Logger.LogInfo($"{nameof(TorProcessManager)} is initialized.");

				#endregion TorProcessInitialization

				cancel.ThrowIfCancellationRequested();

				#region BitcoinStoreInitialization

				try
				{
					await bstoreInitTask.ConfigureAwait(false);

					// Make sure that the height of the wallets will not be better than the current height of the filters.
					WalletManager.SetMaxBestHeight(BitcoinStore.IndexStore.SmartHeaderChain.TipHeight);
				}
				catch (Exception ex) when (!(ex is OperationCanceledException))
				{
					// If our internal data structures in the Bitcoin Store gets corrupted, then it's better to rescan all the wallets.
					WalletManager.SetMaxBestHeight(SmartHeader.GetStartingHeader(Network).Height);
					throw;
				}

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
									mempoolReplacement: "fee,optin",
									userAgent: $"/WasabiClient:{Constants.ClientVersion}/",
									fallbackFee: null, // ToDo: Maybe we should have it, not only for tests?
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

				AddressManagerBehavior addressManagerBehavior = await addrManTask.ConfigureAwait(false);
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
					Nodes = CreateAndConfigureNodesGroup(connectionParameters);
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
					BitcoinStore.BlockRepository);

				#endregion Blocks provider

				WalletManager.RegisterServices(BitcoinStore, Synchronizer, Nodes, Config.ServiceConfiguration, FeeProviders, blockProvider);
			}
			finally
			{
				InitializationCompleted = true;
			}
		}

		private NodesGroup CreateAndConfigureNodesGroup(NodeConnectionParameters connectionParameters)
		{
			var maximumNodeConnection = 12;
			var bestEffortEndpointConnector = new BestEffortEndpointConnector(maximumNodeConnection / 2);
			connectionParameters.EndpointConnector = bestEffortEndpointConnector;
			if (Config.UseTor)
			{
				connectionParameters.TemplateBehaviors.Add(new SocksSettingsBehavior(Config.TorSocks5EndPoint, onlyForOnionHosts: false, networkCredential: null, streamIsolation: true));
			}
			var nodes = new NodesGroup(Network, connectionParameters, requirements: Constants.NodeRequirements);
			nodes.ConnectedNodes.Added += ConnectedNodes_OnAddedOrRemoved;
			nodes.ConnectedNodes.Removed += ConnectedNodes_OnAddedOrRemoved;
			nodes.MaximumNodeConnection = maximumNodeConnection;
			return nodes;
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
					AddressManager = await NBitcoinHelpers.LoadAddressManagerFromPeerFileAsync(AddressManagerFilePath).ConfigureAwait(false);

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

		private void ConnectedNodes_OnAddedOrRemoved(object? sender, NodeEventArgs e)
		{
			if (Nodes.NodeConnectionParameters.EndpointConnector is BestEffortEndpointConnector bestEffortEndPointConnector)
			{
				if (sender is NodesCollection nodesCollection)
				{
					bestEffortEndPointConnector.UpdateConnectedNodesCounter(nodesCollection.Count);
				}
			}
		}

		private void WalletManager_OnDequeue(object sender, DequeueResult e)
		{
			try
			{
				if (UiConfig.LurkingWifeMode)
				{
					return;
				}

				foreach (var success in e.Successful.Where(x => x.Value.Any()))
				{
					DequeueReason reason = success.Key;
					if (reason != DequeueReason.Spent)
					{
						var type = reason == DequeueReason.UserRequested ? NotificationType.Information : NotificationType.Warning;
						var message = reason == DequeueReason.UserRequested ? "" : reason.ToFriendlyString();
						var title = success.Value.Count() == 1 ? $"Coin ({success.Value.First().Amount.ToString(false, true)}) Dequeued" : $"{success.Value.Count()} Coins Dequeued";
						NotificationHelpers.Notify(message, title, type, sender: sender);
					}
				}

				foreach (var failure in e.Unsuccessful.Where(x => x.Value.Any()))
				{
					DequeueReason reason = failure.Key;
					var type = NotificationType.Warning;
					var message = reason.ToFriendlyString();
					var title = failure.Value.Count() == 1 ? $"Couldn't Dequeue Coin ({failure.Value.First().Amount.ToString(false, true)})" : $"Couldn't Dequeue {failure.Value.Count()} Coins";
					NotificationHelpers.Notify(message, title, type, sender: sender);
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}
		}

		private void WalletManager_WalletRelevantTransactionProcessed(object sender, ProcessedResult e)
		{
			try
			{
				// In lurking wife mode no notification is raised.
				// If there are no news, then don't bother too.
				if (UiConfig.LurkingWifeMode || !e.IsNews || (sender as Wallet).State != WalletState.Started)
				{
					return;
				}

				// ToDo
				// Double spent.
				// Anonymity set gained?
				// Received dust

				bool isSpent = e.NewlySpentCoins.Any();
				bool isReceived = e.NewlyReceivedCoins.Any();
				bool isConfirmedReceive = e.NewlyConfirmedReceivedCoins.Any();
				bool isConfirmedSpent = e.NewlyConfirmedReceivedCoins.Any();
				Money miningFee = e.Transaction.Transaction.GetFee(e.SpentCoins.Select(x => x.GetCoin()).ToArray());
				if (isReceived || isSpent)
				{
					Money receivedSum = e.NewlyReceivedCoins.Sum(x => x.Amount);
					Money spentSum = e.NewlySpentCoins.Sum(x => x.Amount);
					Money incoming = receivedSum - spentSum;
					Money receiveSpentDiff = incoming.Abs();
					string amountString = receiveSpentDiff.ToString(false, true);

					if (e.Transaction.Transaction.IsCoinBase)
					{
						NotifyAndLog($"{amountString} BTC", "Mined", NotificationType.Success, e, sender);
					}
					else if (isSpent && receiveSpentDiff == miningFee)
					{
						NotifyAndLog($"Mining Fee: {amountString} BTC", "Self Spend", NotificationType.Information, e, sender);
					}
					else if (isSpent && receiveSpentDiff.Almost(Money.Zero, Money.Coins(0.01m)) && e.IsLikelyOwnCoinJoin)
					{
						NotifyAndLog($"CoinJoin Completed!", "", NotificationType.Success, e, sender);
					}
					else if (incoming > Money.Zero)
					{
						if (e.Transaction.IsRBF && e.Transaction.IsReplacement)
						{
							NotifyAndLog($"{amountString} BTC", "Received Replaceable Replacement Transaction", NotificationType.Information, e, sender);
						}
						else if (e.Transaction.IsRBF)
						{
							NotifyAndLog($"{amountString} BTC", "Received Replaceable Transaction", NotificationType.Success, e, sender);
						}
						else if (e.Transaction.IsReplacement)
						{
							NotifyAndLog($"{amountString} BTC", "Received Replacement Transaction", NotificationType.Information, e, sender);
						}
						else
						{
							NotifyAndLog($"{amountString} BTC", "Received", NotificationType.Success, e, sender);
						}
					}
					else if (incoming < Money.Zero)
					{
						NotifyAndLog($"{amountString} BTC", "Sent", NotificationType.Information, e, sender);
					}
				}
				else if (isConfirmedReceive || isConfirmedSpent)
				{
					Money receivedSum = e.ReceivedCoins.Sum(x => x.Amount);
					Money spentSum = e.SpentCoins.Sum(x => x.Amount);
					Money incoming = receivedSum - spentSum;
					Money receiveSpentDiff = incoming.Abs();
					string amountString = receiveSpentDiff.ToString(false, true);

					if (isConfirmedSpent && receiveSpentDiff == miningFee)
					{
						NotifyAndLog($"Mining Fee: {amountString} BTC", "Self Spend Confirmed", NotificationType.Information, e, sender);
					}
					else if (isConfirmedSpent && receiveSpentDiff.Almost(Money.Zero, Money.Coins(0.01m)) && e.IsLikelyOwnCoinJoin)
					{
						NotifyAndLog($"CoinJoin Confirmed!", "", NotificationType.Information, e, sender);
					}
					else if (incoming > Money.Zero)
					{
						NotifyAndLog($"{amountString} BTC", "Receive Confirmed", NotificationType.Information, e, sender);
					}
					else if (incoming < Money.Zero)
					{
						NotifyAndLog($"{amountString} BTC", "Send Confirmed", NotificationType.Information, e, sender);
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
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

		private static void NotifyAndLog(string message, string title, NotificationType notificationType, ProcessedResult e, object sender)
		{
			message = Guard.Correct(message);
			title = Guard.Correct(title);
			NotificationHelpers.Notify(message, title, notificationType, async () => await FileHelpers.OpenFileInTextEditorAsync(Logger.FilePath).ConfigureAwait(false), sender);
			Logger.LogInfo($"Transaction Notification ({notificationType}): {title} - {message} - {e.Transaction.GetHash()}");
		}

		/// <summary>
		/// 0: nobody called
		/// 1: somebody called
		/// 2: call finished
		/// </summary>
		private long _dispose = 0; // To detect redundant calls

		public async Task DisposeAsync()
		{
			var compareRes = Interlocked.CompareExchange(ref _dispose, 1, 0);
			if (compareRes == 1)
			{
				while (Interlocked.Read(ref _dispose) != 2)
				{
					await Task.Delay(50).ConfigureAwait(false);
				}
				return;
			}
			else if (compareRes == 2)
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

				WalletManager.OnDequeue -= WalletManager_OnDequeue;
				WalletManager.WalletRelevantTransactionProcessed -= WalletManager_WalletRelevantTransactionProcessed;

				var rpcServer = RpcServer;
				if (rpcServer is { })
				{
					using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(21));
					await rpcServer.StopAsync(cts.Token).ConfigureAwait(false);
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
					await synchronizer.StopAsync().ConfigureAwait(false);
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

				if (Nodes is { } nodes)
				{
					nodes.ConnectedNodes.Added -= ConnectedNodes_OnAddedOrRemoved;
					nodes.ConnectedNodes.Removed -= ConnectedNodes_OnAddedOrRemoved;
					nodes.Disconnect();
					while (nodes.ConnectedNodes.Any(x => x.IsConnected))
					{
						await Task.Delay(50).ConfigureAwait(false);
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
					await torManager.StopAsync().ConfigureAwait(false);
					Logger.LogInfo($"{nameof(TorManager)} is stopped.");
				}

				var cache = Cache;
				if (cache is { })
				{
					cache.Dispose();
				}

				try
				{
					SingleInstanceChecker?.Dispose();
				}
				catch (Exception ex)
				{
					Logger.LogError($"Error during the disposal of {nameof(SingleInstanceChecker)}: {ex}");
				}

				if (AsyncMutex.IsAny)
				{
					try
					{
						await AsyncMutex.WaitForAllMutexToCloseAsync().ConfigureAwait(false);
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
				Interlocked.Exchange(ref _dispose, 2);
				Logger.LogSoftwareStopped("Wasabi");
			}
		}
	}
}
