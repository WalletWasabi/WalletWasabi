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

		public static Config Config { get; private set; }

		public async static Task InitializeAsync(Config config)
		{
			Config = Guard.NotNull(nameof(config), config);
			Network network = Config.Network;

			var addressManagerFolderPath = Path.Combine(DataDir, "AddressManager");
			var addressManagerFilePath = Path.Combine(addressManagerFolderPath, $"AddressManager{network}.dat");
			var blocksFolderPath = Path.Combine(DataDir, $"Blocks{network}");
			var connectionParameters = new NodeConnectionParameters();
			AddressManager addressManager = null;
			try
			{
				addressManager = AddressManager.LoadPeerFile(addressManagerFilePath);
				Logger.LogInfo<AddressManager>($"Loaded {nameof(AddressManager)} from `{addressManagerFilePath}`.");
			}
			catch (DirectoryNotFoundException ex)
			{
				Logger.LogInfo<AddressManager>($"{nameof(AddressManager)} did not exist at `{addressManagerFilePath}`. Initializing new one.");
				Logger.LogTrace<AddressManager>(ex);
				addressManager = new AddressManager();
			}
			catch (FileNotFoundException ex)
			{
				Logger.LogInfo<AddressManager>($"{nameof(AddressManager)} did not exist at `{addressManagerFilePath}`. Initializing new one.");
				Logger.LogTrace<AddressManager>(ex);
				addressManager = new AddressManager();
			}

			connectionParameters.TemplateBehaviors.Add(new AddressManagerBehavior(addressManager));
			var memPoolService = new MemPoolService();
			connectionParameters.TemplateBehaviors.Add(new MemPoolBehavior(memPoolService));

			var nodes = new NodesGroup(network, connectionParameters,
				new NodeRequirement
				{
					RequiredServices = NodeServices.Network,
					MinVersion = Constants.ProtocolVersion_WITNESS_VERSION
				});
		}
	}
}
