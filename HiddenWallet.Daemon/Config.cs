using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;

namespace HiddenWallet.Daemon
{
	public static class Config
	{
		// Initialized with default attributes
		public static string WalletFilePath = Path.Combine("Wallets", "Wallet.json");

		public static Network Network = Network.Main;
		public static bool CanSpendUnconfirmed = false;

		static Config()
		{
			if (!File.Exists(ConfigFileSerializer.ConfigFilePath))
			{
				Save();
				Debug.WriteLine($"{ConfigFileSerializer.ConfigFilePath} was missing. It has been created created with default settings.");
			}
			Load();
		}

		public static void Save()
		{
            ConfigFileSerializer.Serialize(
                WalletFilePath,
                Network.ToString(),
                CanSpendUnconfirmed.ToString());
			Load();
		}

		public static void Load()
		{
			var rawContent = ConfigFileSerializer.Deserialize();

			WalletFilePath = rawContent.WalletFilePath;			

			if (rawContent.Network == null)
				throw new NotSupportedException($"Network is missing from {ConfigFileSerializer.ConfigFilePath}");
			string networkString = rawContent.Network.Trim();
			if (networkString == "")
				throw new NotSupportedException($"Network is missing from {ConfigFileSerializer.ConfigFilePath}");
			else if ("mainnet".Equals(networkString, StringComparison.OrdinalIgnoreCase)
				|| "main".Equals(networkString, StringComparison.OrdinalIgnoreCase))
				Network = Network.Main;
			else if ("testnet".Equals(networkString, StringComparison.OrdinalIgnoreCase)
				|| "test".Equals(networkString, StringComparison.OrdinalIgnoreCase))
				Network = Network.TestNet;
			else
				throw new NotSupportedException($"Wrong Network is specified in {ConfigFileSerializer.ConfigFilePath}");

			if (rawContent.CanSpendUnconfirmed == null)
				throw new NotSupportedException($"CanSpendUnconfirmed is missing from {ConfigFileSerializer.ConfigFilePath}");
			string canSpendUnconfirmedString = rawContent.CanSpendUnconfirmed.Trim();
			if (canSpendUnconfirmedString == "")
				throw new NotSupportedException($"CanSpendUnconfirmed is missing from {ConfigFileSerializer.ConfigFilePath}");
			else if ("true".Equals(canSpendUnconfirmedString, StringComparison.OrdinalIgnoreCase)
				|| "yes".Equals(canSpendUnconfirmedString, StringComparison.OrdinalIgnoreCase)
				|| "fuckyeah".Equals(canSpendUnconfirmedString, StringComparison.OrdinalIgnoreCase)
				|| "1" == canSpendUnconfirmedString)
				CanSpendUnconfirmed = true;
			else if ("false".Equals(canSpendUnconfirmedString, StringComparison.OrdinalIgnoreCase)
				|| "no".Equals(canSpendUnconfirmedString, StringComparison.OrdinalIgnoreCase)
				|| "nah".Equals(canSpendUnconfirmedString, StringComparison.OrdinalIgnoreCase)
				|| "0" == canSpendUnconfirmedString)
				CanSpendUnconfirmed = false;
			else
				throw new NotSupportedException($"Wrong CanSpendUnconfirmed value in {ConfigFileSerializer.ConfigFilePath}");            
		}

		public class ConfigFileSerializer
		{
			public static readonly string ConfigFilePath = "Config.json";

			// KEEP THEM PUBLIC OTHERWISE IT WILL NOT SERIALIZE!
			public string WalletFilePath { get; set; }

			public string Network { get; set; }
			public string CanSpendUnconfirmed { get; set; }

			[JsonConstructor]
			private ConfigFileSerializer(
				string walletFilePath,
				string network,
				string canSpendUnconfirmed)
			{
				WalletFilePath = walletFilePath;
				Network = network;
				CanSpendUnconfirmed = canSpendUnconfirmed;
			}

			internal static void Serialize(
				string walletFilePath,
				string network,
				string canSpendUnconfirmed)
			{
				var content =
					JsonConvert.SerializeObject(
						new ConfigFileSerializer(
							walletFilePath,
							network,
							canSpendUnconfirmed), Formatting.Indented);

				File.WriteAllText(ConfigFilePath, content);
			}

			internal static ConfigFileSerializer Deserialize()
			{
				if (!File.Exists(ConfigFilePath))
					throw new Exception($"Config file does not exist. Create {ConfigFilePath} before reading it.");

				var contentString = File.ReadAllText(ConfigFilePath);
				var configFileSerializer = JsonConvert.DeserializeObject<ConfigFileSerializer>(contentString);

				return new ConfigFileSerializer(
					configFileSerializer.WalletFilePath,
					configFileSerializer.Network,
					configFileSerializer.CanSpendUnconfirmed);
			}
		}
	}
}
