using NBitcoin;
using NBitcoin.RPC;
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
		[JsonConverter(typeof(EndPointJsonConverter), Constants.DefaultMainNetBitcoinP2pPort)]
		public EndPoint MainNetBitcoinP2pEndPoint { get; internal set; }

		[JsonProperty(PropertyName = "TestNetBitcoinP2pEndPoint")]
		[JsonConverter(typeof(EndPointJsonConverter), Constants.DefaultTestNetBitcoinP2pPort)]
		public EndPoint TestNetBitcoinP2pEndPoint { get; internal set; }

		[JsonProperty(PropertyName = "RegTestBitcoinP2pEndPoint")]
		[JsonConverter(typeof(EndPointJsonConverter), Constants.DefaultRegTestBitcoinP2pPort)]
		public EndPoint RegTestBitcoinP2pEndPoint { get; internal set; }

		[JsonProperty(PropertyName = "MainNetBitcoinCoreRpcEndPoint")]
		[JsonConverter(typeof(EndPointJsonConverter), Constants.DefaultMainNetBitcoinCoreRpcPort)]
		public EndPoint MainNetBitcoinCoreRpcEndPoint { get; internal set; }

		[JsonProperty(PropertyName = "TestNetBitcoinCoreRpcEndPoint")]
		[JsonConverter(typeof(EndPointJsonConverter), Constants.DefaultTestNetBitcoinCoreRpcPort)]
		public EndPoint TestNetBitcoinCoreRpcEndPoint { get; internal set; }

		[JsonProperty(PropertyName = "RegTestBitcoinCoreRpcEndPoint")]
		[JsonConverter(typeof(EndPointJsonConverter), Constants.DefaultRegTestBitcoinCoreRpcPort)]
		public EndPoint RegTestBitcoinCoreRpcEndPoint { get; internal set; }

		public EndPoint GetBitcoinP2pEndPoint()
		{
			if (Network == Network.Main)
			{
				return MainNetBitcoinP2pEndPoint;
			}
			else if (Network == Network.TestNet)
			{
				return TestNetBitcoinP2pEndPoint;
			}
			else if (Network == Network.RegTest)
			{
				return RegTestBitcoinP2pEndPoint;
			}
			else
			{
				throw new NotSupportedException($"{nameof(Network)} not supported: {Network}.");
			}
		}

		public EndPoint GetBitcoinCoreRpcEndPoint()
		{
			if (Network == Network.Main)
			{
				return MainNetBitcoinCoreRpcEndPoint;
			}
			else if (Network == Network.TestNet)
			{
				return TestNetBitcoinCoreRpcEndPoint;
			}
			else if (Network == Network.RegTest)
			{
				return RegTestBitcoinCoreRpcEndPoint;
			}
			else
			{
				throw new NotSupportedException($"{nameof(Network)} not supported: {Network}.");
			}
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
			EndPoint regTestBitcoinP2pEndPoint,
			EndPoint mainNetBitcoinCoreRpcEndPoint,
			EndPoint testNetBitcoinCoreRpcEndPoint,
			EndPoint regTestBitcoinCoreRpcEndPoint)
		{
			Network = Guard.NotNull(nameof(network), network);
			BitcoinRpcConnectionString = Guard.NotNullOrEmptyOrWhitespace(nameof(bitcoinRpcConnectionString), bitcoinRpcConnectionString);

			MainNetBitcoinP2pEndPoint = Guard.NotNull(nameof(mainNetBitcoinP2pEndPoint), mainNetBitcoinP2pEndPoint);
			TestNetBitcoinP2pEndPoint = Guard.NotNull(nameof(testNetBitcoinP2pEndPoint), testNetBitcoinP2pEndPoint);
			RegTestBitcoinP2pEndPoint = Guard.NotNull(nameof(regTestBitcoinP2pEndPoint), regTestBitcoinP2pEndPoint);

			MainNetBitcoinCoreRpcEndPoint = Guard.NotNull(nameof(mainNetBitcoinCoreRpcEndPoint), mainNetBitcoinCoreRpcEndPoint);
			TestNetBitcoinCoreRpcEndPoint = Guard.NotNull(nameof(testNetBitcoinCoreRpcEndPoint), testNetBitcoinCoreRpcEndPoint);
			RegTestBitcoinCoreRpcEndPoint = Guard.NotNull(nameof(regTestBitcoinCoreRpcEndPoint), regTestBitcoinCoreRpcEndPoint);
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

			MainNetBitcoinP2pEndPoint = new IPEndPoint(IPAddress.Loopback, Constants.DefaultMainNetBitcoinP2pPort);
			TestNetBitcoinP2pEndPoint = new IPEndPoint(IPAddress.Loopback, Constants.DefaultTestNetBitcoinP2pPort);
			RegTestBitcoinP2pEndPoint = new IPEndPoint(IPAddress.Loopback, Constants.DefaultRegTestBitcoinP2pPort);

			MainNetBitcoinCoreRpcEndPoint = new IPEndPoint(IPAddress.Loopback, Constants.DefaultMainNetBitcoinCoreRpcPort);
			TestNetBitcoinCoreRpcEndPoint = new IPEndPoint(IPAddress.Loopback, Constants.DefaultTestNetBitcoinCoreRpcPort);
			RegTestBitcoinCoreRpcEndPoint = new IPEndPoint(IPAddress.Loopback, Constants.DefaultRegTestBitcoinCoreRpcPort);

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

				MainNetBitcoinCoreRpcEndPoint = config.MainNetBitcoinCoreRpcEndPoint ?? MainNetBitcoinCoreRpcEndPoint;
				TestNetBitcoinCoreRpcEndPoint = config.TestNetBitcoinCoreRpcEndPoint ?? TestNetBitcoinCoreRpcEndPoint;
				RegTestBitcoinCoreRpcEndPoint = config.RegTestBitcoinCoreRpcEndPoint ?? RegTestBitcoinCoreRpcEndPoint;

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
			try
			{
				var jsObject = JsonConvert.DeserializeObject<JObject>(jsonString);
				bool saveIt = false;

				var mainNetBitcoinCoreHost = jsObject.Value<string>("MainNetBitcoinCoreHost");
				var mainNetBitcoinCorePort = jsObject.Value<int?>("MainNetBitcoinCorePort");
				var testNetBitcoinCoreHost = jsObject.Value<string>("TestNetBitcoinCoreHost");
				var testNetBitcoinCorePort = jsObject.Value<int?>("TestNetBitcoinCorePort");
				var regTestBitcoinCoreHost = jsObject.Value<string>("RegTestBitcoinCoreHost");
				var regTestBitcoinCorePort = jsObject.Value<int?>("RegTestBitcoinCorePort");

				if (mainNetBitcoinCoreHost != null)
				{
					int port = mainNetBitcoinCorePort ?? Constants.DefaultMainNetBitcoinP2pPort;

					if (EndPointParser.TryParse(mainNetBitcoinCoreHost, port, out EndPoint ep))
					{
						MainNetBitcoinP2pEndPoint = ep;
						saveIt = true;
					}
				}

				if (testNetBitcoinCoreHost != null)
				{
					int port = testNetBitcoinCorePort ?? Constants.DefaultTestNetBitcoinP2pPort;

					if (EndPointParser.TryParse(testNetBitcoinCoreHost, port, out EndPoint ep))
					{
						TestNetBitcoinP2pEndPoint = ep;
						saveIt = true;
					}
				}

				if (regTestBitcoinCoreHost != null)
				{
					int port = regTestBitcoinCorePort ?? Constants.DefaultRegTestBitcoinP2pPort;

					if (EndPointParser.TryParse(regTestBitcoinCoreHost, port, out EndPoint ep))
					{
						RegTestBitcoinP2pEndPoint = ep;
						saveIt = true;
					}
				}

				return saveIt;
			}
			catch (Exception ex)
			{
				Logger.LogWarning<Config>("Backwards compatibility couldn't be ensured.");
				Logger.LogInfo<Config>(ex);
				return false;
			}
		}
	}
}
