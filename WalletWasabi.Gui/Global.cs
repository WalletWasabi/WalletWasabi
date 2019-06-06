using Avalonia;
using Avalonia.Threading;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using NBitcoin.Protocol.Connectors;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Crypto;
using WalletWasabi.Gui.Dialogs;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Helpers;
using WalletWasabi.Hwi;
using WalletWasabi.KeyManagement;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.TorSocks5;

namespace WalletWasabi.Gui
{
	public static class Global
	{
		public static string DataDir { get; }
		public static string TorLogsFile { get; }
		public static string WalletsDir { get; }
		public static string WalletBackupsDir { get; }

		public static BitcoinStore BitcoinStore { get; private set; }
		public static Config Config { get; private set; }

		public static string AddressManagerFilePath { get; private set; }
		public static AddressManager AddressManager { get; private set; }
		public static MemPoolService MemPoolService { get; private set; }

		public static NodesGroup Nodes { get; private set; }
		public static WasabiSynchronizer Synchronizer { get; private set; }
		public static CcjClient ChaumianClient { get; private set; }
		public static WalletService WalletService { get; private set; }
		public static Node RegTestMemPoolServingNode { get; private set; }
		public static UpdateChecker UpdateChecker { get; private set; }
		public static TorProcessManager TorManager { get; private set; }

		public static bool KillRequested { get; private set; } = false;

		public static UiConfig UiConfig { get; private set; }

		public static Network Network => Config.Network;

		static Global()
		{
			DataDir = EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Client"));
			TorLogsFile = Path.Combine(DataDir, "TorLogs.txt");
			WalletsDir = Path.Combine(DataDir, "Wallets");
			WalletBackupsDir = Path.Combine(DataDir, "WalletBackups");

			Directory.CreateDirectory(DataDir);
			Directory.CreateDirectory(WalletsDir);
			Directory.CreateDirectory(WalletBackupsDir);
		}

		public static void InitializeUiConfig(UiConfig uiConfig)
		{
			UiConfig = Guard.NotNull(nameof(uiConfig), uiConfig);
		}

		private static int IsDesperateDequeuing = 0;

		public static async Task TryDesperateDequeueAllCoinsAsync()
		{
			// If already desperate dequeueing then return.
			// If not desperate dequeueing then make sure we're doing that.
			if (Interlocked.CompareExchange(ref IsDesperateDequeuing, 1, 0) == 1)
			{
				return;
			}
			try
			{
				await DesperateDequeueAllCoinsAsync();
			}
			catch (NotSupportedException ex)
			{
				Logger.LogWarning(ex.Message, nameof(Global));
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex, nameof(Global));
			}
			finally
			{
				Interlocked.Exchange(ref IsDesperateDequeuing, 0);
			}
		}

		public static async Task DesperateDequeueAllCoinsAsync()
		{
			if (WalletService is null || ChaumianClient is null)
			{
				return;
			}

			SmartCoin[] enqueuedCoins = WalletService.Coins.Where(x => x.CoinJoinInProgress).ToArray();
			if (enqueuedCoins.Any())
			{
				Logger.LogWarning("Unregistering coins in CoinJoin process.", nameof(Global));
				await ChaumianClient.DequeueCoinsFromMixAsync(enqueuedCoins, "Process was signaled to kill.");
			}
		}

		private static bool Initialized { get; set; } = false;

		public static async Task InitializeNoWalletAsync()
		{
			WalletService = null;
			ChaumianClient = null;
			AddressManager = null;
			TorManager = null;

			#region ConfigInitialization

			Config = new Config(Path.Combine(DataDir, "Config.json"));
			await Config.LoadOrCreateDefaultFileAsync();
			Logger.LogInfo<Config>("Config is successfully initialized.");

			#endregion ConfigInitialization

			BitcoinStore = new BitcoinStore();
			var bstoreInitTask = BitcoinStore.InitializeAsync(Path.Combine(DataDir, "BitcoinStore"), Network);
			var hwiInitTask = HwiProcessManager.InitializeAsync(DataDir, Network);
			var addressManagerFolderPath = Path.Combine(DataDir, "AddressManager");
			AddressManagerFilePath = Path.Combine(addressManagerFolderPath, $"AddressManager{Network}.dat");
			var addrManTask = InitializeAddressManagerBehaviorAsync();

			var blocksFolderPath = Path.Combine(DataDir, $"Blocks{Network}");
			var connectionParameters = new NodeConnectionParameters();

			if (Config.UseTor.Value)
			{
				Synchronizer = new WasabiSynchronizer(Network, BitcoinStore, () => Config.GetCurrentBackendUri(), Config.GetTorSocks5EndPoint());
			}
			else
			{
				Synchronizer = new WasabiSynchronizer(Network, BitcoinStore, Config.GetFallbackBackendUri(), null);
			}

			UpdateChecker = new UpdateChecker(Synchronizer.WasabiClient);

			#region ProcessKillSubscription

			AppDomain.CurrentDomain.ProcessExit += async (s, e) => await TryDesperateDequeueAllCoinsAsync();
			Console.CancelKeyPress += async (s, e) =>
			{
				e.Cancel = true;
				Logger.LogWarning("Process was signaled for killing.", nameof(Global));

				KillRequested = true;
				await TryDesperateDequeueAllCoinsAsync();
				Dispatcher.UIThread.PostLogException(() =>
				{
					Application.Current?.MainWindow?.Close();
				});
				await DisposeAsync();

				Logger.LogInfo($"Wasabi stopped gracefully.", Logger.InstanceGuid.ToString());
			};

			#endregion ProcessKillSubscription

			#region TorProcessInitialization

			if (Config.UseTor.Value)
			{
				TorManager = new TorProcessManager(Config.GetTorSocks5EndPoint(), TorLogsFile);
			}
			else
			{
				TorManager = TorProcessManager.Mock();
			}
			TorManager.Start(false, DataDir);

			var fallbackRequestTestUri = new Uri(Config.GetFallbackBackendUri(), "/api/software/versions");
			TorManager.StartMonitor(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(7), DataDir, fallbackRequestTestUri);

			Logger.LogInfo<TorProcessManager>($"{nameof(TorProcessManager)} is initialized.");

			#endregion TorProcessInitialization

			#region MempoolInitialization

			MemPoolService = new MemPoolService();
			connectionParameters.TemplateBehaviors.Add(new MemPoolBehavior(MemPoolService));

			#endregion MempoolInitialization

			#region HwiProcessInitialization

			try
			{
				await hwiInitTask;
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, nameof(Global));
			}

			#endregion HwiProcessInitialization

			#region BitcoinStoreInitialization

			await bstoreInitTask;

			#endregion BitcoinStoreInitialization

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
					Node node = await Node.ConnectAsync(Network.RegTest, new IPEndPoint(IPAddress.Loopback, 18444));
					Nodes.ConnectedNodes.Add(node);

					RegTestMemPoolServingNode = await Node.ConnectAsync(Network.RegTest, new IPEndPoint(IPAddress.Loopback, 18444));

					RegTestMemPoolServingNode.Behaviors.Add(new MemPoolBehavior(MemPoolService));
				}
				catch (SocketException ex)
				{
					Logger.LogError(ex, nameof(Global));
				}
			}
			else
			{
				if (Config.UseTor is true)
				{
					// onlyForOnionHosts: false - Connect to clearnet IPs through Tor, too.
					connectionParameters.TemplateBehaviors.Add(new SocksSettingsBehavior(Config.GetTorSocks5EndPoint(), onlyForOnionHosts: false, networkCredential: null, streamIsolation: true));
					// allowOnlyTorEndpoints: true - Connect only to onions and don't connect to clearnet IPs at all.
					// This of course makes the first setting unneccessary, but it's better if that's around, in case someone wants to tinker here.
					connectionParameters.EndpointConnector = new DefaultEndpointConnector(allowOnlyTorEndpoints: Network == Network.Main);

					await AddKnownBitcoinFullNodeAsHiddenServiceAsync(AddressManager);
				}
				Nodes = new NodesGroup(Network, connectionParameters, requirements: Constants.NodeRequirements);

				RegTestMemPoolServingNode = null;
			}

			Nodes.Connect();
			Logger.LogInfo("Start connecting to nodes...");

			if (RegTestMemPoolServingNode != null)
			{
				RegTestMemPoolServingNode.VersionHandshake();
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

			Initialized = true;
		}

		private static async Task<AddressManagerBehavior> InitializeAddressManagerBehaviorAsync()
		{
			var needsToDiscoverPeers = true;
			if (Network == Network.RegTest)
			{
				AddressManager = new AddressManager();
				Logger.LogInfo<AddressManager>($"Fake {nameof(AddressManager)} is initialized on the RegTest.");
			}
			else
			{
				try
				{
					AddressManager = await NBitcoinHelpers.LoadAddressManagerFromPeerFileAsync(AddressManagerFilePath);

					// The most of the times we don't need to discover new peers. Instead, we can connect to
					// some of those that we already discovered in the past. In this case we assume that we
					// assume that discovering new peers could be necessary if out address manager has less
					// than 500 addresses. A 500 addresses could be okay because previously we tried with
					// 200 and only one user reported he/she was not able to connect (there could be many others,
					// of course).
					// On the other side, increasing this number forces users that do not need to discover more peers
					// to spend resources (CPU/bandwith) to discover new peers.
					needsToDiscoverPeers = Config.UseTor == true || AddressManager.Count < 500;
					Logger.LogInfo<AddressManager>($"Loaded {nameof(AddressManager)} from `{AddressManagerFilePath}`.");
				}
				catch (DirectoryNotFoundException ex)
				{
					Logger.LogInfo<AddressManager>($"{nameof(AddressManager)} did not exist at `{AddressManagerFilePath}`. Initializing new one.");
					Logger.LogTrace<AddressManager>(ex);
					AddressManager = new AddressManager();
				}
				catch (FileNotFoundException ex)
				{
					Logger.LogInfo<AddressManager>($"{nameof(AddressManager)} did not exist at `{AddressManagerFilePath}`. Initializing new one.");
					Logger.LogTrace<AddressManager>(ex);
					AddressManager = new AddressManager();
				}
				catch (OverflowException ex)
				{
					// https://github.com/zkSNACKs/WalletWasabi/issues/712
					Logger.LogInfo<AddressManager>($"{nameof(AddressManager)} has thrown `{nameof(OverflowException)}`. Attempting to autocorrect.");
					File.Delete(AddressManagerFilePath);
					Logger.LogTrace<AddressManager>(ex);
					AddressManager = new AddressManager();
					Logger.LogInfo<AddressManager>($"{nameof(AddressManager)} autocorrection is successful.");
				}
				catch (FormatException ex)
				{
					// https://github.com/zkSNACKs/WalletWasabi/issues/880
					Logger.LogInfo<AddressManager>($"{nameof(AddressManager)} has thrown `{nameof(FormatException)}`. Attempting to autocorrect.");
					File.Delete(AddressManagerFilePath);
					Logger.LogTrace<AddressManager>(ex);
					AddressManager = new AddressManager();
					Logger.LogInfo<AddressManager>($"{nameof(AddressManager)} autocorrection is successful.");
				}
			}

			var addressManagerBehavior = new AddressManagerBehavior(AddressManager) {
				Mode = needsToDiscoverPeers ? AddressManagerBehaviorMode.Discover : AddressManagerBehaviorMode.None
			};
			return addressManagerBehavior;
		}

		private static async Task AddKnownBitcoinFullNodeAsHiddenServiceAsync(AddressManager addressManager)
		{
			if (Network == Network.RegTest)
			{
				return;
			}

			//  curl -s https://bitnodes.21.co/api/v1/snapshots/latest/ | egrep -o '[a-z0-9]{16}\.onion:?[0-9]*' | sort -ru
			// Then filtered to include only /Satoshi:0.17.x
			var fullBaseDirectory = Path.GetFullPath(AppContext.BaseDirectory);
			if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				if (!fullBaseDirectory.StartsWith('/'))
				{
					fullBaseDirectory.Insert(0, "/");
				}
			}

			var onions = await File.ReadAllLinesAsync(Path.Combine(fullBaseDirectory, "OnionSeeds", $"{Network}OnionSeeds.txt"));

			onions.Shuffle();
			foreach (var onion in onions.Take(60))
			{
				if (NBitcoin.Utils.TryParseEndpoint(onion, Network.DefaultPort, out var endpoint))
				{
					await addressManager.AddAsync(endpoint);
				}
			}
		}

		private static CancellationTokenSource CancelWalletServiceInitialization = null;

		public static async Task InitializeWalletServiceAsync(KeyManager keyManager)
		{
			using (CancelWalletServiceInitialization = new CancellationTokenSource())
			{
				var token = CancelWalletServiceInitialization.Token;
				while (!Initialized)
				{
					await Task.Delay(100, token);
				}

				if (Config.UseTor.Value)
				{
					ChaumianClient = new CcjClient(Synchronizer, Network, keyManager, () => Config.GetCurrentBackendUri(), Config.GetTorSocks5EndPoint());
				}
				else
				{
					ChaumianClient = new CcjClient(Synchronizer, Network, keyManager, Config.GetFallbackBackendUri(), null);
				}
				WalletService = new WalletService(BitcoinStore, keyManager, Synchronizer, ChaumianClient, MemPoolService, Nodes, DataDir, Config.ServiceConfiguration);

				ChaumianClient.Start();
				Logger.LogInfo("Start Chaumian CoinJoin service...");

				Logger.LogInfo("Starting WalletService...");
				await WalletService.InitializeAsync(token);
				Logger.LogInfo("WalletService started.");

				token.ThrowIfCancellationRequested();
				WalletService.Coins.CollectionChanged += Coins_CollectionChanged;
			}
			CancelWalletServiceInitialization = null; // Must make it null explicitly, because dispose won't make it null.
		}

		public static string GetWalletFullPath(string walletName)
		{
			walletName = walletName.TrimEnd(".json", StringComparison.OrdinalIgnoreCase);
			return Path.Combine(WalletsDir, walletName + ".json");
		}

		public static string GetWalletBackupFullPath(string walletName)
		{
			walletName = walletName.TrimEnd(".json", StringComparison.OrdinalIgnoreCase);
			return Path.Combine(WalletBackupsDir, walletName + ".json");
		}

		public static KeyManager LoadKeyManager(string walletFullPath, string walletBackupFullPath)
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
					$"Exception: {ex.ToString()}");
				if (File.Exists(walletFullPath))
				{
					string corruptedWalletBackupPath = Path.Combine(WalletBackupsDir, $"{Path.GetFileName(walletFullPath)}_CorruptedBackup");
					if (File.Exists(corruptedWalletBackupPath))
					{
						File.Delete(corruptedWalletBackupPath);
						Logger.LogInfo($"Deleted previous corrupted wallet file backup from {corruptedWalletBackupPath}.");
					}
					File.Move(walletFullPath, corruptedWalletBackupPath);
					Logger.LogInfo($"Backed up corrupted wallet file to {corruptedWalletBackupPath}.");
				}
				File.Copy(walletBackupFullPath, walletFullPath);

				return LoadKeyManager(walletFullPath);
			}
		}

		public static KeyManager LoadKeyManager(string walletFullPath)
		{
			KeyManager keyManager;

			// Set the LastAccessTime.
			new FileInfo(walletFullPath) {
				LastAccessTime = DateTime.Now
			};

			keyManager = KeyManager.FromFile(walletFullPath);
			Logger.LogInfo($"Wallet loaded: {Path.GetFileNameWithoutExtension(keyManager.FilePath)}.");
			return keyManager;
		}

		private static void Coins_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			try
			{
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || UiConfig?.LurkingWifeMode.Value is true)
				{
					return;
				}

				if (e.Action == NotifyCollectionChangedAction.Add)
				{
					foreach (SmartCoin coin in e.NewItems)
					{
						//if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && RuntimeInformation.OSDescription.StartsWith("Microsoft Windows 10"))
						//{
						//	// It's harder than you'd think. Maybe the best would be to wait for .NET Core 3 for WPF things on Windows?
						//}
						// else

						string amountString = coin.Amount.ToString(false, true);
						using (var process = Process.Start(new ProcessStartInfo {
							FileName = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osascript" : "notify-send",
							Arguments = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? $"-e \"display notification \\\"Received {amountString} BTC\\\" with title \\\"Wasabi\\\"\"" : $"--expire-time=3000 \"Wasabi\" \"Received {amountString} BTC\"",
							CreateNoWindow = true
						})) { }
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex, nameof(Global));
			}
		}

		public static async Task DisposeInWalletDependentServicesAsync()
		{
			if (WalletService != null)
			{
				WalletService.Coins.CollectionChanged -= Coins_CollectionChanged;
			}
			try
			{
				CancelWalletServiceInitialization?.Cancel();
			}
			catch (ObjectDisposedException)
			{
				Logger.LogWarning($"{nameof(CancelWalletServiceInitialization)} is disposed. This can occur due to an error while processing the wallet.", nameof(Global));
			}
			CancelWalletServiceInitialization = null;

			if (WalletService != null)
			{
				if (WalletService.KeyManager != null) // This should not ever happen.
				{
					string backupWalletFilePath = Path.Combine(WalletBackupsDir, Path.GetFileName(WalletService.KeyManager.FilePath));
					WalletService.KeyManager?.ToFile(backupWalletFilePath);
					Logger.LogInfo($"{nameof(KeyManager)} backup saved to {backupWalletFilePath}.", nameof(Global));
				}
				WalletService?.Dispose();
				WalletService = null;
				Logger.LogInfo($"{nameof(WalletService)} is stopped.", nameof(Global));
			}

			if (ChaumianClient != null)
			{
				await ChaumianClient.StopAsync();
				ChaumianClient = null;
				Logger.LogInfo($"{nameof(ChaumianClient)} is stopped.", nameof(Global));
			}
		}

		/// <summary>
		/// 0: nobody called
		/// 1: somebody called
		/// 2: call finished
		/// </summary>
		private static long Dispose = 0; // To detect redundant calls

		public static async Task DisposeAsync()
		{
			var compareRes = Interlocked.CompareExchange(ref Dispose, 1, 0);
			if (compareRes == 1)
			{
				while (Interlocked.Read(ref Dispose) != 2)
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
				await DisposeInWalletDependentServicesAsync();

				if (UpdateChecker != null)
				{
					await UpdateChecker?.StopAsync();
					Logger.LogInfo($"{nameof(UpdateChecker)} is stopped.", nameof(Global));
				}

				if (Synchronizer != null)
				{
					await Synchronizer?.StopAsync();
					Logger.LogInfo($"{nameof(Synchronizer)} is stopped.", nameof(Global));
				}

				if (AddressManagerFilePath != null)
				{
					IoHelpers.EnsureContainingDirectoryExists(AddressManagerFilePath);
					if (AddressManager != null)
					{
						AddressManager?.SavePeerFile(AddressManagerFilePath, Config.Network);
						Logger.LogInfo($"{nameof(AddressManager)} is saved to `{AddressManagerFilePath}`.", nameof(Global));
					}
				}

				if (Nodes != null)
				{
					Nodes?.Disconnect();
					while (Nodes.ConnectedNodes.Any(x => x.IsConnected))
					{
						await Task.Delay(50);
					}
					Nodes?.Dispose();
					Logger.LogInfo($"{nameof(Nodes)} are disposed.", nameof(Global));
				}

				if (RegTestMemPoolServingNode != null)
				{
					RegTestMemPoolServingNode.Disconnect();
					Logger.LogInfo($"{nameof(RegTestMemPoolServingNode)} is disposed.", nameof(Global));
				}

				if (TorManager != null)
				{
					await TorManager?.StopAsync();
					Logger.LogInfo($"{nameof(TorManager)} is stopped.", nameof(Global));
				}

				if (AsyncMutex.IsAny)
				{
					try
					{
						await AsyncMutex.WaitForAllMutexToCloseAsync();
						Logger.LogInfo($"{nameof(AsyncMutex)}(es) are stopped.", nameof(Global));
					}
					catch (Exception ex)
					{
						Logger.LogError($"Error during stopping {nameof(AsyncMutex)}: {ex}", nameof(Global));
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex, nameof(Global));
			}
			finally
			{
				Interlocked.Exchange(ref Dispose, 2);
			}
		}
	}
}
