using Avalonia;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using Org.BouncyCastle.Math;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Crypto;
using WalletWasabi.Helpers;
using WalletWasabi.KeyManagement;
using WalletWasabi.Logging;
using WalletWasabi.Services;

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

		public static string WalletsDir => Path.Combine(DataDir, "Wallets");
		public static string BlocksDir => Path.Combine(DataDir, "Blocks", Config.Network.ToString());
		public static Network Network => Config.Network;

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

		public static Config Config { get; private set; }

		public static void Initialize(Config config)
		{
			WalletService = null;
			ChaumianClient = null;

			Config = Guard.NotNull(nameof(config), config);

			var addressManagerFolderPath = Path.Combine(DataDir, "AddressManager");
			AddressManagerFilePath = Path.Combine(addressManagerFolderPath, $"AddressManager{Network}.dat");
			var blocksFolderPath = Path.Combine(DataDir, $"Blocks{Network}");
			var connectionParameters = new NodeConnectionParameters();
			AddressManager = null;

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

			var indexFilePath = Path.Combine(DataDir, $"Index{Network}.dat");
			IndexDownloader = new IndexDownloader(Network, indexFilePath, Config.GetCurrentBackendUri());

			UpdateChecker = new UpdateChecker(IndexDownloader.WasabiClient);

			Nodes.Connect();
			Logger.LogInfo("Start connecting to nodes...");

			if (RegTestMemPoolServingNode != null)
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
			ChaumianClient = new CcjClient(Network, BlindingPubKey, keyManager, Config.GetCurrentBackendUri());
			WalletService = new WalletService(keyManager, IndexDownloader, ChaumianClient, MemPoolService, Nodes, BlocksDir);

			ChaumianClient.Start();
			Logger.LogInfo("Start Chaumian CoinJoin service...");

			using (CancelWalletServiceInitialization = new CancellationTokenSource())
			{
				Logger.LogInfo("Starting WalletService...");
				await WalletService.InitializeAsync(CancelWalletServiceInitialization.Token);
				Logger.LogInfo("WalletService started.");
			}
			CancelWalletServiceInitialization = null; // Must make it null explicitly, because dispose won't make it null.
		}

		public static async Task DisposeInWalletDependentServicesAsync()
		{
			CancelWalletServiceInitialization?.Cancel();
			CancelWalletServiceInitialization = null;

			WalletService?.Dispose();
			WalletService = null;
			Logger.LogInfo($"{nameof(WalletService)} is stopped.", nameof(Global));

			if (ChaumianClient != null)
			{
				await ChaumianClient.StopAsync();
				ChaumianClient = null;
			}
			Logger.LogInfo($"{nameof(ChaumianClient)} is stopped.", nameof(Global));
		}

		public static async Task DisposeAsync()
		{
			CancelWalletServiceInitialization?.Cancel();

			WalletService?.Dispose();
			Logger.LogInfo($"{nameof(WalletService)} is stopped.", nameof(Global));

			UpdateChecker?.Dispose();
			Logger.LogInfo($"{nameof(UpdateChecker)} is stopped.", nameof(Global));

			IndexDownloader?.Dispose();
			Logger.LogInfo($"{nameof(IndexDownloader)} is stopped.", nameof(Global));

			Directory.CreateDirectory(Path.GetDirectoryName(AddressManagerFilePath));
			AddressManager?.SavePeerFile(AddressManagerFilePath, Config.Network);
			Logger.LogInfo($"{nameof(AddressManager)} is saved to `{AddressManagerFilePath}`.", nameof(Global));

			Nodes?.Dispose();
			Logger.LogInfo($"{nameof(Nodes)} are disposed.", nameof(Global));

			if (RegTestMemPoolServingNode != null)
			{
				RegTestMemPoolServingNode.Disconnect();
				Logger.LogInfo($"{nameof(RegTestMemPoolServingNode)} is disposed.", nameof(Global));
			}

			if (ChaumianClient != null)
			{
				await ChaumianClient.StopAsync();
			}
			Logger.LogInfo($"{nameof(ChaumianClient)} is stopped.", nameof(Global));
		}
	}
}
