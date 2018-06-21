using Avalonia;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
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

		public static string AddressManagerFilePath { get; private set; }
		public static AddressManager AddressManager { get; private set; }
		public static MemPoolService MemPoolService { get; private set; }
		public static NodesGroup Nodes { get; private set; }
		public static IndexDownloader IndexDownloader { get; private set; }

		public static Config Config { get; private set; }

		public static void Initialize(Config config)
		{
			Config = Guard.NotNull(nameof(config), config);
			Network network = Config.Network;

			var addressManagerFolderPath = Path.Combine(DataDir, "AddressManager");
			AddressManagerFilePath = Path.Combine(addressManagerFolderPath, $"AddressManager{network}.dat");
			var blocksFolderPath = Path.Combine(DataDir, $"Blocks{network}");
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

			Nodes = new NodesGroup(network, connectionParameters,
				new NodeRequirement
				{
					RequiredServices = NodeServices.Network,
					MinVersion = Constants.ProtocolVersion_WITNESS_VERSION
				});

			var indexFilePath = Path.Combine(DataDir, $"Index{network}.dat");
			IndexDownloader = new IndexDownloader(network, indexFilePath, Config.GetCurrentUri());

			Nodes.Connect();
			Logger.LogInfo("Start connecting to nodes...");

			IndexDownloader.Synchronize(requestInterval: TimeSpan.FromSeconds(21));
			Logger.LogInfo("Start synchronizing filters...");
		}

		public static void Dispose()
		{
			// Dispose index downloader service.
			IndexDownloader?.Dispose();
			Logger.LogInfo($"{nameof(IndexDownloader)} is stopped.", nameof(Global));

			Directory.CreateDirectory(Path.GetDirectoryName(AddressManagerFilePath));
			AddressManager?.SavePeerFile(AddressManagerFilePath, Config.Network);
			Logger.LogInfo($"{nameof(AddressManager)} is saved to `{AddressManagerFilePath}`.", nameof(Global));
			Nodes?.Dispose();
			Logger.LogInfo($"{nameof(Nodes)} are disposed.", nameof(Global));
		}
	}
}
