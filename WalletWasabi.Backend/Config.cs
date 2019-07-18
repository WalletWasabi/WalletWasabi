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

		[JsonProperty(PropertyName = "MainNetBitcoinP2pEndPoint")]
		[JsonConverter(typeof(EndPointJsonConverter), Constants.DefaultMainNetBintcoinP2pPort)]
		public EndPoint MainNetBitcoinP2pEndPoint { get; internal set; }

		[JsonProperty(PropertyName = "TestNetBitcoinP2pEndPoint")]
		[JsonConverter(typeof(EndPointJsonConverter), Constants.DefaultTestNetBintcoinP2pPort)]
		public EndPoint TestNetBitcoinP2pEndPoint { get; internal set; }

		[JsonProperty(PropertyName = "RegTestBitcoinP2pEndPoint")]
		[JsonConverter(typeof(EndPointJsonConverter), Constants.DefaultRegTestBintcoinP2pPort)]
		public EndPoint RegTestBitcoinP2pEndPoint { get; internal set; }

		private EndPoint _bitcoinCoreEndPoint;

		public EndPoint GetBitcoinP2pEndPoint()
		{
			if (_bitcoinCoreEndPoint is null)
			{
				if (Network == Network.Main)
				{
					_bitcoinCoreEndPoint = MainNetBitcoinP2pEndPoint;
				}
				else if (Network == Network.TestNet)
				{
					_bitcoinCoreEndPoint = TestNetBitcoinP2pEndPoint;
				}
				else if (Network == Network.RegTest)
				{
					_bitcoinCoreEndPoint = RegTestBitcoinP2pEndPoint;
				}
				else
				{
					throw new NotSupportedException("Network not supported.");
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
			EndPoint mainNetBitcoinP2pEndPoint,
			EndPoint testNetBitcoinP2pEndPoint,
			EndPoint regTestBitcoinP2pEndPoint)
		{
			Network = Guard.NotNull(nameof(network), network);
			BitcoinRpcConnectionString = Guard.NotNullOrEmptyOrWhitespace(nameof(bitcoinRpcConnectionString), bitcoinRpcConnectionString);

			MainNetBitcoinP2pEndPoint = Guard.NotNull(nameof(mainNetBitcoinP2pEndPoint), mainNetBitcoinP2pEndPoint);
			TestNetBitcoinP2pEndPoint = Guard.NotNull(nameof(testNetBitcoinP2pEndPoint), testNetBitcoinP2pEndPoint);
			RegTestBitcoinP2pEndPoint = Guard.NotNull(nameof(regTestBitcoinP2pEndPoint), regTestBitcoinP2pEndPoint);
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

			MainNetBitcoinP2pEndPoint = new IPEndPoint(IPAddress.Loopback, Constants.DefaultMainNetBintcoinP2pPort);
			TestNetBitcoinP2pEndPoint = new IPEndPoint(IPAddress.Loopback, Constants.DefaultTestNetBintcoinP2pPort);
			RegTestBitcoinP2pEndPoint = new IPEndPoint(IPAddress.Loopback, Constants.DefaultRegTestBintcoinP2pPort);

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

				MainNetBitcoinP2pEndPoint = config.MainNetBitcoinP2pEndPoint ?? MainNetBitcoinP2pEndPoint;
				TestNetBitcoinP2pEndPoint = config.TestNetBitcoinP2pEndPoint ?? TestNetBitcoinP2pEndPoint;
				RegTestBitcoinP2pEndPoint = config.RegTestBitcoinP2pEndPoint ?? RegTestBitcoinP2pEndPoint;

				if (TryEnsureBackwardsCompatibility(jsonString))
				{
					await ToFileAsync();
				}
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

		private bool TryEnsureBackwardsCompatibility(string jsonString)
		{
			var jsObject = JsonConvert.DeserializeObject<JObject>(jsonString);
			bool saveIt = false;

			if (jsObject.TryGetValue("MainNetBitcoinCoreHost", out JToken jMainNetBitcoinCoreHost))
			{
				int port = Constants.DefaultMainNetBintcoinP2pPort;
				if (jsObject.TryGetValue("MainNetBitcoinCorePort", out JToken jMainNetBitcoinCorePort) && int.TryParse(jMainNetBitcoinCorePort.ToString(), out int p))
				{
					port = p;
				}

				if (EndPointParser.TryParse(jMainNetBitcoinCoreHost.ToString(), port, out EndPoint ep))
				{
					MainNetBitcoinP2pEndPoint = ep;
					saveIt = true;
				}
			}

			if (jsObject.TryGetValue("TestNetBitcoinCoreHost", out JToken jTestNetBitcoinCoreHost))
			{
				int port = Constants.DefaultTestNetBintcoinP2pPort;
				if (jsObject.TryGetValue("TestNetBitcoinCorePort", out JToken jTestNetBitcoinCorePort) && int.TryParse(jTestNetBitcoinCorePort.ToString(), out int p))
				{
					port = p;
				}

				if (EndPointParser.TryParse(jTestNetBitcoinCoreHost.ToString(), port, out EndPoint ep))
				{
					TestNetBitcoinP2pEndPoint = ep;
					saveIt = true;
				}
			}

			if (jsObject.TryGetValue("RegTestBitcoinCoreHost", out JToken jRegTestBitcoinCoreHost))
			{
				int port = Constants.DefaultRegTestBintcoinP2pPort;
				if (jsObject.TryGetValue("RegTestBitcoinCorePort", out JToken jRegTestBitcoinCorePort) && int.TryParse(jRegTestBitcoinCorePort.ToString(), out int p))
				{
					port = p;
				}

				if (EndPointParser.TryParse(jRegTestBitcoinCoreHost.ToString(), port, out EndPoint ep))
				{
					RegTestBitcoinP2pEndPoint = ep;
					saveIt = true;
				}
			}

			return saveIt;
		}
	}
}
