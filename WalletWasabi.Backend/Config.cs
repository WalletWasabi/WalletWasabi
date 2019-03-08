using NBitcoin;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Interfaces;
using WalletWasabi.JsonConverters;
using WalletWasabi.Logging;

namespace WalletWasabi.Backend
{
	[JsonObject(MemberSerialization.OptIn)]
	public class Config : IConfig
	{
		/// <inheritdoc />
		public string FilePath { get; private set; }

		[JsonProperty(PropertyName = "Network")]
		[JsonConverter(typeof(NetworkJsonConverter))]
		public Network Network { get; private set; }

		[JsonProperty(PropertyName = "BitcoinRpcUser")]
		public string BitcoinRpcUser { get; private set; }

		[JsonProperty(PropertyName = "BitcoinRpcPassword")]
		public string BitcoinRpcPassword { get; private set; }

		[JsonProperty(PropertyName = "MainNetBitcoinCoreHost")]
		public string MainNetBitcoinCoreHost { get; internal set; }

		[JsonProperty(PropertyName = "TestNetBitcoinCoreHost")]
		public string TestNetBitcoinCoreHost { get; internal set; }

		[JsonProperty(PropertyName = "RegTestBitcoinCoreHost")]
		public string RegTestBitcoinCoreHost { get; internal set; }

		[JsonProperty(PropertyName = "MainNetBitcoinCorePort")]
		public int? MainNetBitcoinCorePort { get; internal set; }

		[JsonProperty(PropertyName = "TestNetBitcoinCorePort")]
		public int? TestNetBitcoinCorePort { get; internal set; }

		[JsonProperty(PropertyName = "RegTestBitcoinCorePort")]
		public int? RegTestBitcoinCorePort { get; internal set; }

		private IPEndPoint _bitcoinCoreEndPoint;

		public IPEndPoint GetBitcoinCoreEndPoint()
		{
			if (_bitcoinCoreEndPoint is null)
			{
				IPAddress host;
				int? port;
				if (Network == Network.Main)
				{
					host = IPAddress.Parse(MainNetBitcoinCoreHost);
					port = MainNetBitcoinCorePort;
				}
				else if (Network == Network.TestNet)
				{
					host = IPAddress.Parse(TestNetBitcoinCoreHost);
					port = TestNetBitcoinCorePort;
				}
				else // if (Network == Network.RegTest)
				{
					host = IPAddress.Parse(RegTestBitcoinCoreHost);
					port = RegTestBitcoinCorePort;
				}

				_bitcoinCoreEndPoint = new IPEndPoint(host, port ?? Network.DefaultPort);
			}

			return _bitcoinCoreEndPoint;
		}

		public Config()
		{
		}

		public Config(string filePath)
		{
			SetFilePath(filePath);
		}

		public Config(Network network,
			string bitcoinRpcUser,
			string bitcoinRpcPassword,
			string mainNetBitcoinCoreHost,
			string testNetBitcoinCoreHost,
			string regTestBitcoinCoreHost,
			int? mainNetBitcoinCorePort,
			int? testNetBitcoinCorePort,
			int? regTestBitcoinCorePort)
		{
			Network = Guard.NotNull(nameof(network), network);
			BitcoinRpcUser = Guard.NotNullOrEmptyOrWhitespace(nameof(bitcoinRpcUser), bitcoinRpcUser);
			BitcoinRpcPassword = Guard.NotNull(nameof(bitcoinRpcPassword), bitcoinRpcPassword);

			MainNetBitcoinCoreHost = Guard.NotNullOrEmptyOrWhitespace(nameof(mainNetBitcoinCoreHost), mainNetBitcoinCoreHost);
			TestNetBitcoinCoreHost = Guard.NotNullOrEmptyOrWhitespace(nameof(testNetBitcoinCoreHost), testNetBitcoinCoreHost);
			RegTestBitcoinCoreHost = Guard.NotNullOrEmptyOrWhitespace(nameof(regTestBitcoinCoreHost), regTestBitcoinCoreHost);
			MainNetBitcoinCorePort = Guard.NotNull(nameof(mainNetBitcoinCorePort), mainNetBitcoinCorePort);
			TestNetBitcoinCorePort = Guard.NotNull(nameof(testNetBitcoinCorePort), testNetBitcoinCorePort);
			RegTestBitcoinCorePort = Guard.NotNull(nameof(regTestBitcoinCorePort), regTestBitcoinCorePort);
		}

		/// <inheritdoc />
		public async Task ToFileAsync()
		{
			AssertFilePathSet();

			string jsonString = JsonConvert.SerializeObject(this, Formatting.Indented);
			await File.WriteAllTextAsync(FilePath,
			jsonString,
			Encoding.UTF8);
		}

		/// <inheritdoc />
		public async Task LoadOrCreateDefaultFileAsync()
		{
			AssertFilePathSet();

			Network = Network.Main;
			BitcoinRpcUser = "user";
			BitcoinRpcPassword = "password";

			MainNetBitcoinCoreHost = IPAddress.Loopback.ToString();
			TestNetBitcoinCoreHost = IPAddress.Loopback.ToString();
			RegTestBitcoinCoreHost = IPAddress.Loopback.ToString();
			MainNetBitcoinCorePort = Network.Main.DefaultPort;
			TestNetBitcoinCorePort = Network.TestNet.DefaultPort;
			RegTestBitcoinCorePort = Network.RegTest.DefaultPort;

			if (!File.Exists(FilePath))
			{
				Logger.LogInfo<Config>($"{nameof(Config)} file did not exist. Created at path: `{FilePath}`.");
			}
			else
			{
				string jsonString = await File.ReadAllTextAsync(FilePath, Encoding.UTF8);
				var config = JsonConvert.DeserializeObject<Config>(jsonString);

				Network = config.Network ?? Network;
				BitcoinRpcUser = config.BitcoinRpcUser ?? BitcoinRpcUser;
				BitcoinRpcPassword = config.BitcoinRpcPassword ?? BitcoinRpcPassword;

				MainNetBitcoinCoreHost = config.MainNetBitcoinCoreHost ?? MainNetBitcoinCoreHost;
				TestNetBitcoinCoreHost = config.TestNetBitcoinCoreHost ?? TestNetBitcoinCoreHost;
				RegTestBitcoinCoreHost = config.RegTestBitcoinCoreHost ?? RegTestBitcoinCoreHost;
				MainNetBitcoinCorePort = config.MainNetBitcoinCorePort ?? MainNetBitcoinCorePort;
				TestNetBitcoinCorePort = config.TestNetBitcoinCorePort ?? TestNetBitcoinCorePort;
				RegTestBitcoinCorePort = config.RegTestBitcoinCorePort ?? RegTestBitcoinCorePort;
			}

			await ToFileAsync();
		}

		/// <inheritdoc />
		public async Task<bool> CheckFileChangeAsync()
		{
			AssertFilePathSet();

			if (!File.Exists(FilePath))
			{
				throw new FileNotFoundException($"{nameof(Config)} file did not exist at path: `{FilePath}`.");
			}

			string jsonString = await File.ReadAllTextAsync(FilePath, Encoding.UTF8);
			var config = JsonConvert.DeserializeObject<Config>(jsonString);

			if (Network != config.Network)
			{
				return true;
			}
			if (BitcoinRpcPassword != config.BitcoinRpcPassword)
			{
				return true;
			}
			if (BitcoinRpcUser != config.BitcoinRpcUser)
			{
				return true;
			}

			if (!MainNetBitcoinCoreHost.Equals(config.MainNetBitcoinCoreHost, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			if (!TestNetBitcoinCoreHost.Equals(config.TestNetBitcoinCoreHost, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			if (!RegTestBitcoinCoreHost.Equals(config.RegTestBitcoinCoreHost, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			if (MainNetBitcoinCorePort != config.MainNetBitcoinCorePort)
			{
				return true;
			}
			if (TestNetBitcoinCorePort != config.TestNetBitcoinCorePort)
			{
				return true;
			}
			if (RegTestBitcoinCorePort != config.RegTestBitcoinCorePort)
			{
				return true;
			}

			return false;
		}

		/// <inheritdoc />
		public void SetFilePath(string path)
		{
			FilePath = Guard.NotNullOrEmptyOrWhitespace(nameof(path), path, trim: true);
		}

		/// <inheritdoc />
		public void AssertFilePathSet()
		{
			if (FilePath is null) throw new NotSupportedException($"{nameof(FilePath)} is not set. Use {nameof(SetFilePath)} to set it.");
		}
	}
}
