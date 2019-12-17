using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore;
using WalletWasabi.BitcoinCore.Endpointing;
using WalletWasabi.BitcoinCore.Monitoring;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.CoinJoin.Client;
using WalletWasabi.CoinJoin.Client.Clients;
using WalletWasabi.CoinJoin.Client.Clients.Queuing;
using WalletWasabi.CoinJoin.Client.Rounds;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Helpers;
using WalletWasabi.Hwi.Models;
using WalletWasabi.Logging;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.TorSocks5;

namespace WalletWasabi.Gui
{
	public class Global
	{ 
		public const string ThemeBackgroundBrushResourceKey = "ThemeBackgroundBrush";
		public const string ApplicationAccentForegroundBrushResourceKey = "ApplicationAccentForegroundBrush";

		public string DataDir { get; }
		public string TorLogsFile { get; }
		public string WalletsDir { get; }
		public string WalletBackupsDir { get; }

		public BitcoinStore BitcoinStore { get; private set; }
		public Config Config { get; private set; }

		public string AddressManagerFilePath { get; private set; }
		public AddressManager AddressManager { get; private set; }

		public NodesGroup Nodes { get; private set; }
		public WasabiSynchronizer Synchronizer { get; private set; }
		public FeeProviders FeeProviders { get; private set; }
		public CoinJoinClient ChaumianClient { get; private set; }
		public WalletService WalletService { get; private set; }
		public TransactionBroadcaster TransactionBroadcaster { get; set; }
		public CoinJoinProcessor CoinJoinProcessor { get; set; }
		public Node RegTestMempoolServingNode { get; private set; }
		public TorProcessManager TorManager { get; private set; }
		public CoreNode BitcoinCoreNode { get; private set; }

		public HostedServices HostedServices { get; }

		public bool KillRequested { get; private set; } = false;

		public UiConfig UiConfig { get; private set; }

		public Network Network => Config.Network;

		public Global()
		{
			DataDir = EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Client"));
			TorLogsFile = Path.Combine(DataDir, "TorLogs.txt");
			WalletsDir = Path.Combine(DataDir, "Wallets");
			WalletBackupsDir = Path.Combine(DataDir, "WalletBackups");

			Directory.CreateDirectory(DataDir);
			Directory.CreateDirectory(WalletsDir);
			Directory.CreateDirectory(WalletBackupsDir);

			HostedServices = new HostedServices();
		}

		public void InitializeUiConfig(UiConfig uiConfig)
		{
			UiConfig = Guard.NotNull(nameof(uiConfig), uiConfig);
		}

		private int _isDesperateDequeuing = 0;

		public async Task TryDesperateDequeueAllCoinsAsync()
		{
			// If already desperate dequeuing then return.
			// If not desperate dequeuing then make sure we're doing that.
			if (Interlocked.CompareExchange(ref _isDesperateDequeuing, 1, 0) == 1)
			{
				return;
			}
			try
			{
				await DesperateDequeueAllCoinsAsync();
			}
			catch (NotSupportedException ex)
			{
				Logger.LogWarning(ex.Message);
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}
			finally
			{
				Interlocked.Exchange(ref _isDesperateDequeuing, 0);
			}
		}

		public async Task DesperateDequeueAllCoinsAsync()
		{
			if (WalletService is null || ChaumianClient is null)
			{
				return;
			}

			SmartCoin[] enqueuedCoins = WalletService.Coins.CoinJoinInProcess().ToArray();
			if (enqueuedCoins.Any())
			{
				Logger.LogWarning("Unregistering coins in CoinJoin process.");
				await ChaumianClient.DequeueCoinsFromMixAsync(enqueuedCoins, DequeueReason.ApplicationExit);
			}
		}

		private bool InitializationCompleted { get; set; } = false;
		private CancellationTokenSource StoppingCts { get; set; } = new CancellationTokenSource();

		public async Task InitializeNoWalletAsync()
		{
			try
			{
				WalletService = null;
				ChaumianClient = null;
				AddressManager = null;
				TorManager = null;

				#region ConfigInitialization

				Config = new Config(Path.Combine(DataDir, "Config.json"));
				await Config.LoadOrCreateDefaultFileAsync();
				Logger.LogInfo($"{nameof(Config)} is successfully initialized.");

				#endregion ConfigInitialization

				BitcoinStore = new BitcoinStore();
				var bstoreInitTask = BitcoinStore.InitializeAsync(Path.Combine(DataDir, "BitcoinStore"), Network);
				var addressManagerFolderPath = Path.Combine(DataDir, "AddressManager");

				AddressManagerFilePath = Path.Combine(addressManagerFolderPath, $"AddressManager{Network}.dat");
				var addrManTask = InitializeAddressManagerBehaviorAsync();

				var blocksFolderPath = Path.Combine(DataDir, $"Blocks{Network}");
				var connectionParameters = new NodeConnectionParameters { UserAgent = "/Satoshi:0.18.1/" };

				if (Config.UseTor)
				{
					Synchronizer = new WasabiSynchronizer(Network, BitcoinStore, () => Config.GetCurrentBackendUri(), Config.TorSocks5EndPoint);
				}
				else
				{
					Synchronizer = new WasabiSynchronizer(Network, BitcoinStore, Config.GetFallbackBackendUri(), null);
				}

				HostedServices.Register(new UpdateChecker(TimeSpan.FromMinutes(7), Synchronizer.WasabiClient), "Software Update Checker");

				#region ProcessKillSubscription

				AppDomain.CurrentDomain.ProcessExit += async (s, e) => await TryDesperateDequeueAllCoinsAsync();
				Console.CancelKeyPress += async (s, e) =>
				{
					e.Cancel = true;
					Logger.LogWarning("Process was signaled for killing.");

					KillRequested = true;
					await TryDesperateDequeueAllCoinsAsync();
					Dispatcher.UIThread.PostLogException(() =>
					{
						var window = (Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime).MainWindow;
						window?.Close();
					});
					await DisposeAsync();

					Logger.LogSoftwareStopped("Wasabi");
				};

				#endregion ProcessKillSubscription

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

				#region BitcoinStoreInitialization

				await bstoreInitTask;

				#endregion BitcoinStoreInitialization

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
									userAgent: $"/WasabiClient:{Constants.ClientVersion.ToString()}/"),
								CancellationToken.None)
							.ConfigureAwait(false);
					}
				}
				catch (Exception ex)
				{
					Logger.LogError(ex);
				}

				await HostedServices.StartAllAsync(StoppingCts.Token).ConfigureAwait(false);

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

				#region MempoolInitialization

				connectionParameters.TemplateBehaviors.Add(BitcoinStore.CreateUntrustedP2pBehavior());

				#endregion MempoolInitialization

				#region AddressManagerInitialization

				AddressManagerBehavior addressManagerBehavior = await addrManTask;
				connectionParameters.TemplateBehaviors.Add(addressManagerBehavior);

				#endregion AddressManagerInitialization

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
					if (Config.UseTor is true)
					{
						// onlyForOnionHosts: false - Connect to clearnet IPs through Tor, too.
						connectionParameters.TemplateBehaviors.Add(new SocksSettingsBehavior(Config.TorSocks5EndPoint, onlyForOnionHosts: false, networkCredential: null, streamIsolation: true));
						// allowOnlyTorEndpoints: true - Connect only to onions and do not connect to clearnet IPs at all.
						// This of course makes the first setting unnecessary, but it's better if that's around, in case someone wants to tinker here.
						connectionParameters.EndpointConnector = new DefaultEndpointConnector(allowOnlyTorEndpoints: Network == Network.Main);

						await AddKnownBitcoinFullNodeAsHiddenServiceAsync(AddressManager);
					}
					Nodes = new NodesGroup(Network, connectionParameters, requirements: Constants.NodeRequirements);

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

				TransactionBroadcaster = new TransactionBroadcaster(Network, BitcoinStore, Synchronizer, Nodes, BitcoinCoreNode?.RpcClient);
				CoinJoinProcessor = new CoinJoinProcessor(Synchronizer, BitcoinCoreNode?.RpcClient);
			}
			catch (Exception ex)
			{
				Logger.LogCritical(ex);
				InitializationCompleted = true;
				await DisposeAsync().ConfigureAwait(false);
				Environment.Exit(1);
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
					// to spend resources (CPU/bandwith) to discover new peers.
					needsToDiscoverPeers = Config.UseTor is true || AddressManager.Count < 500;
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

		private CancellationTokenSource _cancelWalletServiceInitialization = null;

		public async Task InitializeWalletServiceAsync(KeyManager keyManager)
		{
			using (_cancelWalletServiceInitialization = new CancellationTokenSource())
			{
				var token = _cancelWalletServiceInitialization.Token;
				while (!InitializationCompleted)
				{
					await Task.Delay(100, token);
				}

				if (Config.UseTor)
				{
					ChaumianClient = new CoinJoinClient(Synchronizer, Network, keyManager, () => Config.GetCurrentBackendUri(), Config.TorSocks5EndPoint);
				}
				else
				{
					ChaumianClient = new CoinJoinClient(Synchronizer, Network, keyManager, Config.GetFallbackBackendUri(), null);
				}

				WalletService = new WalletService(BitcoinStore, keyManager, Synchronizer, ChaumianClient, Nodes, DataDir, Config.ServiceConfiguration, FeeProviders, BitcoinCoreNode);

				ChaumianClient.Start();
				Logger.LogInfo("Start Chaumian CoinJoin service...");

				Logger.LogInfo($"Starting {nameof(WalletService)}...");
				await WalletService.InitializeAsync(token);
				Logger.LogInfo($"{nameof(WalletService)} started.");

				token.ThrowIfCancellationRequested();

				TransactionBroadcaster.AddWalletService(WalletService);
				CoinJoinProcessor.AddWalletService(WalletService);

				WalletService.TransactionProcessor.WalletRelevantTransactionProcessed += TransactionProcessor_WalletRelevantTransactionProcessed;
				ChaumianClient.OnDequeue += ChaumianClient_OnDequeued;
			}
			_cancelWalletServiceInitialization = null; // Must make it null explicitly, because dispose won't make it null.
		}

		private void ChaumianClient_OnDequeued(object sender, DequeueResult e)
		{
			try
			{
				if (UiConfig?.LurkingWifeMode is true)
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
						NotificationHelpers.Notify(message, title, type);
					}
				}

				foreach (var failure in e.Unsuccessful.Where(x => x.Value.Any()))
				{
					DequeueReason reason = failure.Key;
					var type = NotificationType.Warning;
					var message = reason.ToFriendlyString();
					var title = failure.Value.Count() == 1 ? $"Couldn't Dequeue Coin ({failure.Value.First().Amount.ToString(false, true)})" : $"Couldn't Dequeue {failure.Value.Count()} Coins";
					NotificationHelpers.Notify(message, title, type);
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}
		}

		private void TransactionProcessor_WalletRelevantTransactionProcessed(object sender, ProcessedResult e)
		{
			try
			{
				// In lurking wife mode no notification is raised.
				// If there are no news, then don't bother too.
				if (UiConfig?.LurkingWifeMode is true || !e.IsNews)
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
						NotifyAndLog($"{amountString} BTC", "Mined", NotificationType.Success, e);
					}
					else if (isSpent && receiveSpentDiff == miningFee)
					{
						NotifyAndLog($"Mining Fee: {amountString} BTC", "Self Spend", NotificationType.Information, e);
					}
					else if (isSpent && receiveSpentDiff.Almost(Money.Zero, Money.Coins(0.01m)) && e.IsLikelyOwnCoinJoin)
					{
						NotifyAndLog($"CoinJoin Completed!", "", NotificationType.Success, e);
					}
					else if (incoming > Money.Zero)
					{
						if (e.Transaction.IsRBF && e.Transaction.IsReplacement)
						{
							NotifyAndLog($"{amountString} BTC", "Received Replacable Replacement Transaction", NotificationType.Information, e);
						}
						else if (e.Transaction.IsRBF)
						{
							NotifyAndLog($"{amountString} BTC", "Received Replacable Transaction", NotificationType.Success, e);
						}
						else if (e.Transaction.IsReplacement)
						{
							NotifyAndLog($"{amountString} BTC", "Received Replacement Transaction", NotificationType.Information, e);
						}
						else
						{
							NotifyAndLog($"{amountString} BTC", "Received", NotificationType.Success, e);
						}
					}
					else if (incoming < Money.Zero)
					{
						NotifyAndLog($"{amountString} BTC", "Sent", NotificationType.Information, e);
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
						NotifyAndLog($"Mining Fee: {amountString} BTC", "Self Spend Confirmed", NotificationType.Information, e);
					}
					else if (isConfirmedSpent && receiveSpentDiff.Almost(Money.Zero, Money.Coins(0.01m)) && e.IsLikelyOwnCoinJoin)
					{
						NotifyAndLog($"CoinJoin Confirmed!", "", NotificationType.Information, e);
					}
					else if (incoming > Money.Zero)
					{
						NotifyAndLog($"{amountString} BTC", "Receive Confirmed", NotificationType.Information, e);
					}
					else if (incoming < Money.Zero)
					{
						NotifyAndLog($"{amountString} BTC", "Send Confirmed", NotificationType.Information, e);
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}
		}

		private static void NotifyAndLog(string message, string title, NotificationType notificationType, ProcessedResult e)
		{
			message = Guard.Correct(message);
			title = Guard.Correct(title);
			NotificationHelpers.Notify(message, title, notificationType, () => FileHelpers.OpenFileInTextEditor(Logger.FilePath));
			Logger.LogInfo($"Transaction Notification ({notificationType}): {title} - {message} - {e.Transaction.GetHash()}");
		}

		public string GetWalletFullPath(string walletName)
		{
			walletName = walletName.TrimEnd(".json", StringComparison.OrdinalIgnoreCase);
			return Path.Combine(WalletsDir, walletName + ".json");
		}

		public string GetWalletBackupFullPath(string walletName)
		{
			walletName = walletName.TrimEnd(".json", StringComparison.OrdinalIgnoreCase);
			return Path.Combine(WalletBackupsDir, walletName + ".json");
		}

		public KeyManager LoadKeyManager(string walletFullPath, string walletBackupFullPath)
		{
			try
			{
				return LoadKeyManager(walletFullPath);
			}
			catch (Exception ex)
			{
				if (!File.Exists(walletBackupFullPath))
				{
					throw;
				}

				Logger.LogWarning($"Wallet got corrupted.\n" +
					$"Wallet Filepath: {walletFullPath}\n" +
					$"Trying to recover it from backup.\n" +
					$"Backup path: {walletBackupFullPath}\n" +
					$"Exception: {ex}");
				if (File.Exists(walletFullPath))
				{
					string corruptedWalletBackupPath = Path.Combine(WalletBackupsDir, $"{Path.GetFileName(walletFullPath)}_CorruptedBackup");
					if (File.Exists(corruptedWalletBackupPath))
					{
						File.Delete(corruptedWalletBackupPath);
						Logger.LogInfo($"Deleted previous corrupted wallet file backup from `{corruptedWalletBackupPath}`.");
					}
					File.Move(walletFullPath, corruptedWalletBackupPath);
					Logger.LogInfo($"Backed up corrupted wallet file to `{corruptedWalletBackupPath}`.");
				}
				File.Copy(walletBackupFullPath, walletFullPath);

				return LoadKeyManager(walletFullPath);
			}
		}

		public KeyManager LoadKeyManager(string walletFullPath)
		{
			KeyManager keyManager;

			// Set the LastAccessTime.
			new FileInfo(walletFullPath)
			{
				LastAccessTime = DateTime.Now
			};

			keyManager = KeyManager.FromFile(walletFullPath);
			Logger.LogInfo($"Wallet loaded: {Path.GetFileNameWithoutExtension(keyManager.FilePath)}.");
			return keyManager;
		}

		public async Task DisposeInWalletDependentServicesAsync()
		{
			var walletService = WalletService;
			if (walletService is { })
			{
				WalletService.TransactionProcessor.WalletRelevantTransactionProcessed -= TransactionProcessor_WalletRelevantTransactionProcessed;
				ChaumianClient.OnDequeue -= ChaumianClient_OnDequeued;
			}

			try
			{
				_cancelWalletServiceInitialization?.Cancel();
			}
			catch (ObjectDisposedException)
			{
				Logger.LogWarning($"{nameof(_cancelWalletServiceInitialization)} is disposed. This can occur due to an error while processing the wallet.");
			}
			_cancelWalletServiceInitialization = null;

			walletService = WalletService;
			if (walletService is { })
			{
				var keyManager = walletService.KeyManager;
				if (keyManager is { }) // This should never happen.
				{
					string backupWalletFilePath = Path.Combine(WalletBackupsDir, Path.GetFileName(keyManager.FilePath));
					keyManager.ToFile(backupWalletFilePath);
					Logger.LogInfo($"{nameof(walletService.KeyManager)} backup saved to `{backupWalletFilePath}`.");
				}
				walletService.Dispose();
				WalletService = null;
				Logger.LogInfo($"{nameof(WalletService)} is stopped.");
			}

			var chaumianClient = ChaumianClient;
			if (chaumianClient is { })
			{
				await chaumianClient.StopAsync();
				ChaumianClient = null;
				Logger.LogInfo($"{nameof(ChaumianClient)} is stopped.");
			}
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
					await Task.Delay(50);
				}
				return;
			}
			else if (compareRes == 2)
			{
				return;
			}

			try
			{
				StoppingCts?.Cancel();
				while (!InitializationCompleted)
				{
					await Task.Delay(100);
				}

				await DisposeInWalletDependentServicesAsync();

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
				StoppingCts = null;
				Interlocked.Exchange(ref _dispose, 2);
			}
		}

		public string GetNextWalletName()
		{
			for (int i = 0; i < int.MaxValue; i++)
			{
				if (!File.Exists(Path.Combine(WalletsDir, $"Wallet{i}.json")))
				{
					return $"Wallet{i}";
				}
			}

			throw new NotSupportedException("This is impossible.");
		}

		public string GetNextHardwareWalletName(HwiEnumerateEntry hwi = null, string customPrefix = null)
		{
			var prefix = customPrefix is null
				? hwi is null
					? "HardwareWallet"
					: hwi.Model.ToString()
				: customPrefix;

			for (int i = 0; i < int.MaxValue; i++)
			{
				var name = $"{prefix}{i}";
				if (!File.Exists(Path.Combine(WalletsDir, $"{name}.json")))
				{
					return name;
				}
			}

			throw new NotSupportedException("This is impossible.");
		}
	}
}
