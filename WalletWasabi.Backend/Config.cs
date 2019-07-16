using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel;
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

		[JsonProperty(PropertyName = "BitcoinRpcConnectionString")]
		public string BitcoinRpcConnectionString { get; private set; }

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

		private EndPoint _bitcoinCoreEndPoint;

		public EndPoint GetBitcoinCoreEndPoint()
		{
			if (_bitcoinCoreEndPoint is null)
			{
				IPAddress ipHost;
				string dnsHost = null;
				int? port = null;
				try
				{
					if (Network == Network.Main)
					{
						port = MainNetBitcoinCorePort;
						dnsHost = MainNetBitcoinCoreHost;
						ipHost = IPAddress.Parse(MainNetBitcoinCoreHost);
					}
					else if (Network == Network.TestNet)
					{
						port = TestNetBitcoinCorePort;
						dnsHost = TestNetBitcoinCoreHost;
						ipHost = IPAddress.Parse(TestNetBitcoinCoreHost);
					}
					else // if (Network == Network.RegTest)
					{
						port = RegTestBitcoinCorePort;
						dnsHost = RegTestBitcoinCoreHost;
						ipHost = IPAddress.Parse(RegTestBitcoinCoreHost);
					}

					_bitcoinCoreEndPoint = new IPEndPoint(ipHost, port ?? Network.DefaultPort);
				}
				catch
				{
					_bitcoinCoreEndPoint = new DnsEndPoint(dnsHost, port ?? Network.DefaultPort);
				}
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
			string bitcoinRpcConnectionString,
			string mainNetBitcoinCoreHost,
			string testNetBitcoinCoreHost,
			string regTestBitcoinCoreHost,
			int? mainNetBitcoinCorePort,
			int? testNetBitcoinCorePort,
			int? regTestBitcoinCorePort)
		{
			Network = Guard.NotNull(nameof(network), network);
			BitcoinRpcConnectionString = Guard.NotNullOrEmptyOrWhitespace(nameof(bitcoinRpcConnectionString), bitcoinRpcConnectionString);

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
			BitcoinRpcConnectionString = "user:password";

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
				BitcoinRpcConnectionString = config.BitcoinRpcConnectionString ?? BitcoinRpcConnectionString;

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
			var newConfig = JsonConvert.DeserializeObject<JObject>(jsonString);
			var currentConfig = JObject.FromObject(this);
			return !JToken.DeepEquals(newConfig, currentConfig);
		}

		/// <inheritdoc />
		public void SetFilePath(string path)
		{
			FilePath = Guard.NotNullOrEmptyOrWhitespace(nameof(path), path, trim: true);
		}

		/// <inheritdoc />
		public void AssertFilePathSet()
		{
			if (FilePath is null)
			{
				throw new NotSupportedException($"{nameof(FilePath)} is not set. Use {nameof(SetFilePath)} to set it.");
			}
		}
	}
}
