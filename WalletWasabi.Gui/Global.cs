using Avalonia;
using Avalonia.Threading;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using Org.BouncyCastle.Math;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Crypto;
using WalletWasabi.Helpers;
using WalletWasabi.KeyManagement;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.TorSocks5;

namespace WalletWasabi.Gui
{
	public static class Global
	{
		private static string _dataDir = null;

		public static string DataDir
		{
			get
			{
				if (!string.IsNullOrWhiteSpace(_dataDir)) return _dataDir;

				_dataDir = EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Client"));

				return _dataDir;
			}
		}

		private static string _torLogsFile = null;

		public static string TorLogsFile
		{
			get
			{
				if (!string.IsNullOrWhiteSpace(_torLogsFile)) return _torLogsFile;

				_torLogsFile = Path.Combine(DataDir, "TorLogs.txt");

				return _torLogsFile;
			}
		}

		public static string WalletsDir => Path.Combine(DataDir, "Wallets");
		public static string WalletBackupsDir => Path.Combine(DataDir, "WalletBackups");
		public static Network Network => Config.Network;
		public static string IndexFilePath => Path.Combine(DataDir, $"Index{Network}.dat");

		public static BlindingRsaPubKey BlindingPubKey => Config.GetBlindingRsaPubKey();

		public static string AddressManagerFilePath { get; private set; }
		public static AddressManager AddressManager { get; private set; }
		public static MemPoolService MemPoolService { get; private set; }
		public static NodesGroup Nodes { get; private set; }
		public static IndexDownloader IndexDownloader { get; private set; }
		public static CcjClient ChaumianClient { get; private set; }
		public static WalletService WalletService { get; private set; }
		public static Node RegTestMemPoolServingNode { get; private set; }
		public static UpdateChecker UpdateChecker { get; private set; }
		public static TorProcessManager TorManager { get; private set; }

		public static Config Config { get; private set; }

		public static void InitializeConfig(Config config)
		{
			Config = Guard.NotNull(nameof(config), config);
		}

		private static long _triedDesperateDequeuing = 0;

		private static async Task TryDesperateDequeueAllCoinsAsync()
		{
			try
			{
				if (Interlocked.Read(ref _triedDesperateDequeuing) == 1)
				{
					return;
				}
				else
				{
					Interlocked.Increment(ref _triedDesperateDequeuing);
				}

				if (WalletService is null || ChaumianClient is null)
					return;
				SmartCoin[] enqueuedCoins = WalletService.Coins.Where(x => x.CoinJoinInProgress).ToArray();
				if (enqueuedCoins.Any())
				{
					Logger.LogWarning("Unregistering coins in CoinJoin process.", nameof(Global));
					await ChaumianClient.DequeueCoinsFromMixAsync(enqueuedCoins);
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex, nameof(Global));
			}
		}

		public static void InitializeNoWallet()
		{
			WalletService = null;
			ChaumianClient = null;

			AppDomain.CurrentDomain.ProcessExit += async (s, e) => await TryDesperateDequeueAllCoinsAsync();
			Console.CancelKeyPress += async (s, e) =>
			{
				e.Cancel = true;
				Logger.LogWarning("Process was signaled for killing.", nameof(Global));
				await TryDesperateDequeueAllCoinsAsync();
				Dispatcher.UIThread.Post(() =>
				{
					Application.Current.MainWindow.Close();
				});
			};

			var addressManagerFolderPath = Path.Combine(DataDir, "AddressManager");
			AddressManagerFilePath = Path.Combine(addressManagerFolderPath, $"AddressManager{Network}.dat");
			var blocksFolderPath = Path.Combine(DataDir, $"Blocks{Network}");
			var connectionParameters = new NodeConnectionParameters();
			AddressManager = null;
			TorManager = null;

			TorManager = new TorProcessManager(Config.GetTorSocks5EndPoint(), TorLogsFile);
			TorManager.Start(false, DataDir);

			Logger.LogInfo<TorProcessManager>($"{nameof(TorProcessManager)} is initialized.");

			if (Network == Network.RegTest)
			{
				AddressManager = new AddressManager();
				Logger.LogInfo<AddressManager>($"Fake {nameof(AddressManager)} is initialized on the RegTest.");
			}
			else
			{
				try
				{
					AddressManager = AddressManager.LoadPeerFile(AddressManagerFilePath);
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
			}

			connectionParameters.TemplateBehaviors.Add(new AddressManagerBehavior(AddressManager));
			MemPoolService = new MemPoolService();
			connectionParameters.TemplateBehaviors.Add(new MemPoolBehavior(MemPoolService));

			if (Network == Network.RegTest)
			{
				Nodes = new NodesGroup(Network,
					requirements: new NodeRequirement
					{
						RequiredServices = NodeServices.Network,
						MinVersion = Constants.ProtocolVersion_WITNESS_VERSION
					});
				Nodes.ConnectedNodes.Add(Node.Connect(Network.RegTest, new IPEndPoint(IPAddress.Loopback, 18444)));

				RegTestMemPoolServingNode = Node.Connect(Network.RegTest, new IPEndPoint(IPAddress.Loopback, 18444));
				RegTestMemPoolServingNode.Behaviors.Add(new MemPoolBehavior(MemPoolService));
			}
			else
			{
				Nodes = new NodesGroup(Network, connectionParameters,
					new NodeRequirement
					{
						RequiredServices = NodeServices.Network,
						MinVersion = Constants.ProtocolVersion_WITNESS_VERSION
					});

				RegTestMemPoolServingNode = null;
			}

			IndexDownloader = new IndexDownloader(Network, IndexFilePath, Config.GetCurrentBackendUri(), Config.GetTorSocks5EndPoint());

			UpdateChecker = new UpdateChecker(IndexDownloader.WasabiClient);

			Nodes.Connect();
			Logger.LogInfo("Start connecting to nodes...");

			if (!(RegTestMemPoolServingNode is null))
			{
				RegTestMemPoolServingNode.VersionHandshake();
				Logger.LogInfo("Start connecting to mempool serving regtest node...");
			}

			IndexDownloader.Synchronize(requestInterval: TimeSpan.FromSeconds(21));
			Logger.LogInfo("Start synchronizing filters...");
		}

		private static CancellationTokenSource CancelWalletServiceInitialization = null;

		public static async Task InitializeWalletServiceAsync(KeyManager keyManager)
		{
			ChaumianClient = new CcjClient(Network, BlindingPubKey, keyManager, Config.GetCurrentBackendUri(), Config.GetTorSocks5EndPoint());
			WalletService = new WalletService(keyManager, IndexDownloader, ChaumianClient, MemPoolService, Nodes, DataDir);

			ChaumianClient.Start(0, 3);
			Logger.LogInfo("Start Chaumian CoinJoin service...");

			using (CancelWalletServiceInitialization = new CancellationTokenSource())
			{
				Logger.LogInfo("Starting WalletService...");
				await WalletService.InitializeAsync(CancelWalletServiceInitialization.Token);
				Logger.LogInfo("WalletService started.");
			}
			CancelWalletServiceInitialization = null; // Must make it null explicitly, because dispose won't make it null.
			WalletService.CoinReceived += OnCoinReceived;
		}

		private static void OnCoinReceived(object sender, SmartCoin coin)
		{
			try
			{
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				{
					Process.Start(new ProcessStartInfo
					{
						FileName = "notify-send",
						Arguments = $"--expire-time=3000 \"Wasabi\" \"Received {coin.Amount.ToString(false, true)} BTC\"",
						CreateNoWindow = true
					});
				}
				else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				{
					Process.Start(new ProcessStartInfo
					{
						FileName = "osascript",
						Arguments = $"-e display notification \"Received {coin.Amount.ToString(false, true)} BTC\" with title \"Wasabi\"",
						CreateNoWindow = true
					});
				}
				//else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && RuntimeInformation.OSDescription.StartsWith("Microsoft Windows 10"))
				//{
				//	// It's harder than you'd think. Maybe the best would be to wait for .NET Core 3 for WPF things on Windows?
				//}
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
				WalletService.CoinReceived -= OnCoinReceived;
			}
			CancelWalletServiceInitialization?.Cancel();
			CancelWalletServiceInitialization = null;

			if (!(WalletService is null))
			{
				if (!(WalletService.KeyManager is null)) // This should not ever happen.
				{
					string backupWalletFilePath = Path.Combine(WalletBackupsDir, Path.GetFileName(WalletService.KeyManager.FilePath));
					WalletService.KeyManager?.ToFile(backupWalletFilePath);
					Logger.LogInfo($"{nameof(KeyManager)} backup saved to {backupWalletFilePath}.", nameof(Global));
				}
				WalletService.Dispose();
				WalletService = null;
			}

			Logger.LogInfo($"{nameof(WalletService)} is stopped.", nameof(Global));

			if (!(ChaumianClient is null))
			{
				await ChaumianClient.StopAsync();
				ChaumianClient = null;
			}
			Logger.LogInfo($"{nameof(ChaumianClient)} is stopped.", nameof(Global));
		}

		public static async Task DisposeAsync()
		{
			try
			{
				await DisposeInWalletDependentServicesAsync();

				UpdateChecker?.Dispose();
				Logger.LogInfo($"{nameof(UpdateChecker)} is stopped.", nameof(Global));

				IndexDownloader?.Dispose();
				Logger.LogInfo($"{nameof(IndexDownloader)} is stopped.", nameof(Global));

				IoHelpers.EnsureContainingDirectoryExists(AddressManagerFilePath);
				AddressManager?.SavePeerFile(AddressManagerFilePath, Config.Network);
				Logger.LogInfo($"{nameof(AddressManager)} is saved to `{AddressManagerFilePath}`.", nameof(Global));

				Nodes?.Dispose();
				Logger.LogInfo($"{nameof(Nodes)} are disposed.", nameof(Global));

				if (!(RegTestMemPoolServingNode is null))
				{
					RegTestMemPoolServingNode.Disconnect();
					Logger.LogInfo($"{nameof(RegTestMemPoolServingNode)} is disposed.", nameof(Global));
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex, nameof(Global));
			}
		}
	}
}
