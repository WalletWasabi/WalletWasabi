using NBitcoin;
using Newtonsoft.Json;
using System;
using System.IO;

namespace HiddenWallet
{
	public enum ConnectionType
	{
		FullNode,
		Http
	}

	public static class Config
	{
		// Initialized with default attributes
		public static string DefaultWalletFileName = @"Wallet.json";

		public static Network Network = Network.Main;
		public static ConnectionType ConnectionType = ConnectionType.Http;
		public static bool CanSpendUnconfirmed;
		public static bool UseTor;
		public static string TorHost = "127.0.0.1";
		public static int TorSocksPort = 9050;
		public static int TorControlPort = 9051;
		public static string TorControlPortPassword = "ILoveBitcoin21";

		static Config()
		{
			if (!File.Exists(ConfigFileSerializer.ConfigFilePath))
			{
				Save();
				Console.WriteLine($"{ConfigFileSerializer.ConfigFilePath} was missing. It has been created created with default settings.");
			}
			Load();
		}

		public static void Save()
		{
			ConfigFileSerializer.Serialize(
				DefaultWalletFileName,
				Network.ToString(),
				ConnectionType.ToString(),
				CanSpendUnconfirmed.ToString(),
				UseTor.ToString(),
				TorHost,
				TorSocksPort.ToString(),
				TorControlPort.ToString(),
				TorControlPortPassword);
			Load();
		}

		public static void Load()
		{
			var rawContent = ConfigFileSerializer.Deserialize();

			DefaultWalletFileName = rawContent.DefaultWalletFileName;

			if (rawContent.Network == Network.Main.ToString())
				Network = Network.Main;
			else if (rawContent.Network == Network.TestNet.ToString())
				Network = Network.TestNet;
			else if (rawContent.Network == null)
				throw new Exception($"Network is missing from {ConfigFileSerializer.ConfigFilePath}");
			else
				throw new Exception($"Wrong Network is specified in {ConfigFileSerializer.ConfigFilePath}");

			if (rawContent.ConnectionType == ConnectionType.FullNode.ToString())
				ConnectionType = ConnectionType.FullNode;
			else if (rawContent.ConnectionType == ConnectionType.Http.ToString())
				ConnectionType = ConnectionType.Http;
			else if (rawContent.ConnectionType == null)
				throw new Exception($"ConnectionType is missing from {ConfigFileSerializer.ConfigFilePath}");
			else
				throw new Exception($"Wrong ConnectionType is specified in {ConfigFileSerializer.ConfigFilePath}");

			try
			{
				CanSpendUnconfirmed = bool.Parse(rawContent.CanSpendUnconfirmed.Trim());
			}
			catch (Exception ex)
			{
				throw new Exception($"Wrong CanSpendUnconfirmed value in {ConfigFileSerializer.ConfigFilePath}", ex);
			}
			try
			{
				UseTor = bool.Parse(rawContent.UseTor.Trim());
			}
			catch (Exception ex)
			{
				throw new Exception($"Wrong UseTor value in {ConfigFileSerializer.ConfigFilePath}", ex);
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
		public static string ConfigFilePath = "Config.json";

		// KEEP THEM PUBLIC OTHERWISE IT WILL NOT SERIALIZE!
		public string DefaultWalletFileName { get; set; }

		public string Network { get; set; }
		public string ConnectionType { get; set; }
		public string CanSpendUnconfirmed { get; set; }
		public string UseTor { get; set; }
		public string TorHost { get; set; }
		public string TorSocksPort { get; set; }
		public string TorControlPort { get; set; }
		public string TorControlPortPassword { get; set; }

		[JsonConstructor]
		private ConfigFileSerializer(
			string walletFileName,
			string network,
			string connectionType,
			string canSpendUnconfirmed,
			string useTor,
			string torHost,
			string torSocksPort,
			string torControlPort,
			string torControlPortPassword)
		{
			DefaultWalletFileName = walletFileName;
			Network = network;
			ConnectionType = connectionType;
			CanSpendUnconfirmed = canSpendUnconfirmed;
			UseTor = useTor;
			TorHost = torHost;
			TorSocksPort = torSocksPort;
			TorControlPort = torControlPort;
			TorControlPortPassword = torControlPortPassword;
		}

		internal static void Serialize(
			string walletFileName,
			string network,
			string connectionType,
			string canSpendUnconfirmed,
			string useTor,
			string torHost,
			string torSocksPort,
			string torControlPort,
			string torControlPortPassword)
		{
			var content =
				JsonConvert.SerializeObject(
					new ConfigFileSerializer(
						walletFileName,
						network,
						connectionType,
						canSpendUnconfirmed,
						useTor,
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
				configFileSerializer.DefaultWalletFileName,
				configFileSerializer.Network,
				configFileSerializer.ConnectionType,
				configFileSerializer.CanSpendUnconfirmed,
				configFileSerializer.UseTor,
				configFileSerializer.TorHost,
				configFileSerializer.TorSocksPort,
				configFileSerializer.TorControlPort,
				configFileSerializer.TorControlPortPassword);
		}
	}
}