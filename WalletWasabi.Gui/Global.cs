using Avalonia;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using Org.BouncyCastle.Math;
using System;
using System.Collections.Generic;
using System.IO;
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

		public static BlindingRsaPubKey BlindingPubKey { get; private set; }

		public static string AddressManagerFilePath { get; private set; }
		public static AddressManager AddressManager { get; private set; }
		public static MemPoolService MemPoolService { get; private set; }
		public static NodesGroup Nodes { get; private set; }
		public static IndexDownloader IndexDownloader { get; private set; }
		public static CcjClient ChaumianClient { get; private set; }
		public static WalletService WalletService { get; private set; }

		public static Config Config { get; private set; }

		public static void Initialize(Config config)
		{
			WalletService = null;
			ChaumianClient = null;

			Config = Guard.NotNull(nameof(config), config);

			string blindingPubKeyFile = Path.Combine(DataDir, $"BlindingPubKey{Network}.json");
			if (File.Exists(blindingPubKeyFile))
			{
				string blindingPubKeyJson = "";
				blindingPubKeyJson = File.ReadAllText(blindingPubKeyFile);
				BlindingPubKey = BlindingRsaPubKey.CreateFromJson(blindingPubKeyJson);
			}
			else
			{
				if (Network == Network.Main)
				{
					BlindingPubKey = new BlindingRsaPubKey(new BigInteger("16421152619146079007287475569112871971988560541093277613438316709041030720662622782033859387192362542996510605015506477964793447620206674394713753349543444988246276357919473682408472170521463339860947351211455351029147665615454176157348164935212551240942809518428851690991984017733153078846480521091423447691527000770982623947706172997649440619968085147635776736938871139581019988225202983052255684151711253254086264386774936200194229277914886876824852466823571396538091430866082004097086602287294474304344865162932126041736158327600847754258634325228417149098062181558798532036659383679712667027126535424484318399849"), new BigInteger("65537"));
				}
				else
				{
					BlindingPubKey = new BlindingRsaPubKey(new BigInteger("1947359444838071727420232507652169869937347616735925361477589434039008038907229064999576278651443575362470457496666718250346530518268694562965606704838796709743032825816642704620776596590683042135764246115456630753521"), new BigInteger("65537"));
				}
				Directory.CreateDirectory(DataDir);
				File.WriteAllText(blindingPubKeyFile, BlindingPubKey.ToJson());
			}

			var addressManagerFolderPath = Path.Combine(DataDir, "AddressManager");
			AddressManagerFilePath = Path.Combine(addressManagerFolderPath, $"AddressManager{Network}.dat");
			var blocksFolderPath = Path.Combine(DataDir, $"Blocks{Network}");
			var connectionParameters = new NodeConnectionParameters();
			AddressManager = null;
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

			connectionParameters.TemplateBehaviors.Add(new AddressManagerBehavior(AddressManager));
			MemPoolService = new MemPoolService();
			connectionParameters.TemplateBehaviors.Add(new MemPoolBehavior(MemPoolService));

			Nodes = new NodesGroup(Network, connectionParameters,
				new NodeRequirement
				{
					RequiredServices = NodeServices.Network,
					MinVersion = Constants.ProtocolVersion_WITNESS_VERSION
				});

			var indexFilePath = Path.Combine(DataDir, $"Index{Network}.dat");
			IndexDownloader = new IndexDownloader(Network, indexFilePath, Config.GetCurrentBackendUri());

			Nodes.Connect();
			Logger.LogInfo("Start connecting to nodes...");

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

		public static async Task DisposeAsync()
		{
			CancelWalletServiceInitialization?.Cancel();

			WalletService?.Dispose();
			Logger.LogInfo($"{nameof(WalletService)} is stopped.", nameof(Global));

			IndexDownloader?.Dispose();
			Logger.LogInfo($"{nameof(IndexDownloader)} is stopped.", nameof(Global));

			Directory.CreateDirectory(Path.GetDirectoryName(AddressManagerFilePath));
			AddressManager?.SavePeerFile(AddressManagerFilePath, Config.Network);
			Logger.LogInfo($"{nameof(AddressManager)} is saved to `{AddressManagerFilePath}`.", nameof(Global));

			Nodes?.Dispose();
			Logger.LogInfo($"{nameof(Nodes)} are disposed.", nameof(Global));

			if (ChaumianClient != null)
			{
				await ChaumianClient.StopAsync();
			}
			Logger.LogInfo($"{nameof(ChaumianClient)} is stopped.", nameof(Global));
		}
	}
}
