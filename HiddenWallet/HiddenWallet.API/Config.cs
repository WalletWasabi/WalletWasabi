using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;

namespace HiddenWallet.API
{
	public static class Config
	{
		// Initialized with default attributes
		public static string WalletFilePath = @"Wallets\Wallet.json";

		public static Network Network = Network.Main;
		public static bool CanSpendUnconfirmed = false;
		public static string TorHost = "127.0.0.1";
		public static int TorSocksPort = 9050;
		public static int TorControlPort = 9051;
		public static string TorControlPortPassword = "ILoveBitcoin21";

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
				CanSpendUnconfirmed.ToString(),
				TorHost,
				TorSocksPort.ToString(),
				TorControlPort.ToString(),
				TorControlPortPassword);
			Load();
		}

		public static void Load()
		{
			var rawContent = ConfigFileSerializer.Deserialize();

			WalletFilePath = rawContent.WalletFilePath;

			if (rawContent.Network == Network.Main.ToString())
				Network = Network.Main;
			else if (rawContent.Network == Network.TestNet.ToString())
				Network = Network.TestNet;
			else if (rawContent.Network == null)
				throw new Exception($"Network is missing from {ConfigFileSerializer.ConfigFilePath}");
			else
				throw new Exception($"Wrong Network is specified in {ConfigFileSerializer.ConfigFilePath}");

			try
			{
				CanSpendUnconfirmed = bool.Parse(rawContent.CanSpendUnconfirmed.Trim());
			}
			catch (Exception ex)
			{
				throw new Exception($"Wrong CanSpendUnconfirmed value in {ConfigFileSerializer.ConfigFilePath}", ex);
			}

			TorHost = rawContent.TorHost.Trim();

			try
			{
				TorSocksPort = int.Parse(rawContent.TorSocksPort.Trim());
			}
			catch (Exception ex)
			{
				throw new Exception($"Wrong TorSocksPort value in {ConfigFileSerializer.ConfigFilePath}", ex);
			}

			try
			{
				TorControlPort = int.Parse(rawContent.TorControlPort.Trim());
			}
			catch (Exception ex)
			{
				throw new Exception($"Wrong TorControlPort value in {ConfigFileSerializer.ConfigFilePath}", ex);
			}

			TorControlPortPassword = rawContent.TorControlPortPassword;
		}
	}

	public class ConfigFileSerializer
	{
		public static readonly string ConfigFilePath = "Config.json";

		// KEEP THEM PUBLIC OTHERWISE IT WILL NOT SERIALIZE!
		public string WalletFilePath { get; set; }

		public string Network { get; set; }
		public string CanSpendUnconfirmed { get; set; }
		public string TorHost { get; set; }
		public string TorSocksPort { get; set; }
		public string TorControlPort { get; set; }
		public string TorControlPortPassword { get; set; }

		[JsonConstructor]
		private ConfigFileSerializer(
			string walletFilePath,
			string network,
			string canSpendUnconfirmed,
			string torHost,
			string torSocksPort,
			string torControlPort,
			string torControlPortPassword)
		{
			WalletFilePath = walletFilePath;
			Network = network;
			CanSpendUnconfirmed = canSpendUnconfirmed;
			TorHost = torHost;
			TorSocksPort = torSocksPort;
			TorControlPort = torControlPort;
			TorControlPortPassword = torControlPortPassword;
		}

		internal static void Serialize(
			string walletFilePath,
			string network,
			string canSpendUnconfirmed,
			string torHost,
			string torSocksPort,
			string torControlPort,
			string torControlPortPassword)
		{
			var content =
				JsonConvert.SerializeObject(
					new ConfigFileSerializer(
						walletFilePath,
						network,
						canSpendUnconfirmed,
						torHost,
						torSocksPort,
						torControlPort,
						torControlPortPassword), Formatting.Indented);

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
				configFileSerializer.CanSpendUnconfirmed,
				configFileSerializer.TorHost,
				configFileSerializer.TorSocksPort,
				configFileSerializer.TorControlPort,
				configFileSerializer.TorControlPortPassword);
		}
	}
}
