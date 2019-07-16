using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Gui.Models;
using WalletWasabi.Helpers;
using WalletWasabi.Interfaces;
using WalletWasabi.JsonConverters;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.TorSocks5;

namespace WalletWasabi.Gui
{
	[JsonObject(MemberSerialization.OptIn)]
	public class Config : IConfig
	{
		public const int DefaultPrivacyLevelSome = 2;
		public const int DefaultPrivacyLevelFine = 21;
		public const int DefaultPrivacyLevelStrong = 50;
		public const int DefaultMixUntilAnonymitySet = 50;
		public const int DefaultTorSock5Port = 9050;
		public static readonly Money DefaultDustThreshold = Money.Coins(0.0001m);

		/// <inheritdoc />
		public string FilePath { get; private set; }

		[JsonProperty(PropertyName = "Network")]
		[JsonConverter(typeof(NetworkJsonConverter))]
		public Network Network
		{
			get
			{
				return _network ?? Network.Main;
			}
			internal set
			{
				_network = value;
			}
		}
		private Network _network;

		[DefaultValue("http://wasabiukrxmkdgve5kynjztuovbg43uxcbcxn6y2okcrsg7gb6jdmbad.onion/")]
		[JsonProperty(PropertyName = "MainNetBackendUriV3", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string MainNetBackendUriV3 { get; private set; }

		[DefaultValue("http://testwnp3fugjln6vh5vpj7mvq3lkqqwjj3c2aafyu7laxz42kgwh2rad.onion/")]
		[JsonProperty(PropertyName = "TestNetBackendUriV3", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string TestNetBackendUriV3 { get; private set; }

		[DefaultValue("https://wasabiwallet.io/")]
		[JsonProperty(PropertyName = "MainNetFallbackBackendUri", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string MainNetFallbackBackendUri { get; private set; }

		[DefaultValue("https://wasabiwallet.co/")]
		[JsonProperty(PropertyName = "TestNetFallbackBackendUri", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string TestNetFallbackBackendUri { get; private set; }

		[DefaultValue("http://localhost:37127/")]
		[JsonProperty(PropertyName = "RegTestBackendUriV3", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string RegTestBackendUriV3 { get; private set; }

		[DefaultValue(true)]
		[JsonProperty(PropertyName = "UseTor", DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool UseTor { get; internal set; }

		[DefaultValue("127.0.0.1")]
		[JsonProperty(PropertyName = "TorHost", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string TorHost { get; internal set; }

		[DefaultValue(DefaultTorSock5Port)]
		[JsonProperty(PropertyName = "TorSocks5Port", DefaultValueHandling = DefaultValueHandling.Populate)]
		public int TorSocks5Port { get; internal set; }

		[DefaultValue("127.0.0.1")]
		[JsonProperty(PropertyName = "MainNetBitcoinCoreHost", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string MainNetBitcoinCoreHost { get; internal set; }

		[DefaultValue("127.0.0.1")]
		[JsonProperty(PropertyName = "TestNetBitcoinCoreHost", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string TestNetBitcoinCoreHost { get; internal set; }

		[DefaultValue("127.0.0.1")]
		[JsonProperty(PropertyName = "RegTestBitcoinCoreHost", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string RegTestBitcoinCoreHost { get; internal set; }

		[DefaultValue(8333)]
		[JsonProperty(PropertyName = "MainNetBitcoinCorePort", DefaultValueHandling = DefaultValueHandling.Populate)]
		public int MainNetBitcoinCorePort { get; internal set; }

		[DefaultValue(18333)]
		[JsonProperty(PropertyName = "TestNetBitcoinCorePort", DefaultValueHandling = DefaultValueHandling.Populate)]
		public int TestNetBitcoinCorePort { get; internal set; }

		[DefaultValue(18443)]
		[JsonProperty(PropertyName = "RegTestBitcoinCorePort", DefaultValueHandling = DefaultValueHandling.Populate)]
		public int RegTestBitcoinCorePort { get; internal set; }

		[DefaultValue(DefaultMixUntilAnonymitySet)]
		[JsonProperty(PropertyName = "MixUntilAnonymitySet", DefaultValueHandling = DefaultValueHandling.Populate)]
		public int MixUntilAnonymitySet { get; internal set; }

		[DefaultValue(DefaultPrivacyLevelSome)]
		[JsonProperty(PropertyName = "PrivacyLevelSome", DefaultValueHandling = DefaultValueHandling.Populate)]
		public int PrivacyLevelSome { get; internal set; }

		[DefaultValue(DefaultPrivacyLevelFine)]
		[JsonProperty(PropertyName = "PrivacyLevelFine", DefaultValueHandling = DefaultValueHandling.Populate)]
		public int PrivacyLevelFine { get; internal set; }

		[DefaultValue(DefaultPrivacyLevelStrong)]
		[JsonProperty(PropertyName = "PrivacyLevelStrong", DefaultValueHandling = DefaultValueHandling.Populate)]
		public int PrivacyLevelStrong { get; internal set; }

		[JsonProperty(PropertyName = "DustThreshold")]
		[JsonConverter(typeof(MoneyBtcJsonConverter))]
		public Money DustThreshold
		{
			get
			{
				return _dustThreshold ?? DefaultDustThreshold;
			}
			internal set
			{
				_dustThreshold = value;
			}
		}
		private Money _dustThreshold;

		private Uri _backendUri;
		private Uri _fallbackBackendUri;

		public Uri GetCurrentBackendUri()
		{
			if (TorProcessManager.RequestFallbackAddressUsage)
			{
				return GetFallbackBackendUri();
			}

			if (_backendUri != null)
			{
				return _backendUri;
			}

			if (Network == Network.Main)
			{
				_backendUri = new Uri(MainNetBackendUriV3);
			}
			else if (Network == Network.TestNet)
			{
				_backendUri = new Uri(TestNetBackendUriV3);
			}
			else // RegTest
			{
				_backendUri = new Uri(RegTestBackendUriV3);
			}

			return _backendUri;
		}

		public Uri GetFallbackBackendUri()
		{
			if (_fallbackBackendUri != null)
			{
				return _fallbackBackendUri;
			}

			if (Network == Network.Main)
			{
				_fallbackBackendUri = new Uri(MainNetFallbackBackendUri);
			}
			else if (Network == Network.TestNet)
			{
				_fallbackBackendUri = new Uri(TestNetFallbackBackendUri);
			}
			else // RegTest
			{
				_fallbackBackendUri = new Uri(RegTestBackendUriV3);
			}

			return _fallbackBackendUri;
		}

		private IPEndPoint _torSocks5EndPoint;

		public IPEndPoint GetTorSocks5EndPoint()
		{
			if (_torSocks5EndPoint is null)
			{
				var host = IPAddress.Parse(TorHost);
				_torSocks5EndPoint = new IPEndPoint(host, (int)TorSocks5Port);
			}

			return _torSocks5EndPoint;
		}

		public void SetEndpoint(string host, int? port)
		{
			if (Network == Network.Main)
			{
				MainNetBitcoinCoreHost = host;
				MainNetBitcoinCorePort = port ?? Network.Main.DefaultPort;
			}
			else if (Network == Network.TestNet)
			{
				TestNetBitcoinCoreHost = host;
				TestNetBitcoinCorePort = port ?? Network.TestNet.DefaultPort;
			}
			else if (Network == Network.RegTest)
			{
				RegTestBitcoinCoreHost = host;
				RegTestBitcoinCorePort = port ?? Network.RegTest.DefaultPort;
			}
			else
			{
				throw new NotSupportedException($"Unsupported Network");
			}
		}

		public (string Host, int Port) GetEndpoint()
		{
			var host = IPAddress.Loopback.ToString();
			var port = Network.DefaultPort;
			if (Network == Network.Main)
			{
				host = MainNetBitcoinCoreHost ?? host;
				port = MainNetBitcoinCorePort;
			}
			else if (Network == Network.TestNet)
			{
				host = TestNetBitcoinCoreHost ?? host;
				port = TestNetBitcoinCorePort;
			}
			else if (Network == Network.RegTest)
			{
				host = RegTestBitcoinCoreHost ?? host;
				port = RegTestBitcoinCorePort;
			}
			else
			{
				throw new NotSupportedException($"Unsupported Network");
			}
			return (host, port);
		}

		public static bool TryNormalizeP2PHost(string host, int defaultPort, out string result)
		{
			host = host.Trim();
			if (Uri.TryCreate(host, UriKind.Absolute, out var uri) && uri.HostNameType != UriHostNameType.Unknown)
			{
				if (!uri.Scheme.Equals("bitcoin-p2p", StringComparison.OrdinalIgnoreCase) &&
					!uri.Scheme.Equals("tcp", StringComparison.OrdinalIgnoreCase))
				{
					result = string.Empty;
					return false;
				}
				result = uri.Authority;
				return true;
			}
			try
			{
				result = NBitcoin.Utils.ParseEndpoint(host, defaultPort).ToEndpointString();
				return true;
			}
			catch
			{
				result = string.Empty;
				return false;
			}
		}

		public EndPoint GetBitcoinCoreEndPoint()
		{
			var endpoint = GetEndpoint();
			var host = TryNormalizeP2PHost(endpoint.Host, endpoint.Port, out string normalized) ? normalized : IPAddress.Loopback.ToString();
			try
			{
				return NBitcoin.Utils.ParseEndpoint(endpoint.Host, endpoint.Port);
			}
			catch
			{
				return new IPEndPoint(IPAddress.Loopback, endpoint.Port);
			}
		}

		public Config()
		{
			_backendUri = null;
		}

		public Config(string filePath)
		{
			_backendUri = null;
			SetFilePath(filePath);
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
			JsonConvert.PopulateObject("{}", this);

			if (!File.Exists(FilePath))
			{
				Logger.LogInfo<Config>($"{nameof(Config)} file did not exist. Created at path: `{FilePath}`.");
			}
			else
			{
				await LoadFileAsync();
			}
			// Just debug convenience.
			_backendUri = GetCurrentBackendUri();
			await ToFileAsync();
		}

		public ServiceConfiguration GetServiceConfiguration()
		{
			return new ServiceConfiguration(MixUntilAnonymitySet, PrivacyLevelSome, PrivacyLevelFine, PrivacyLevelStrong, GetBitcoinCoreEndPoint(), DustThreshold);
		}

		public async Task LoadFileAsync()
		{
			string jsonString = await File.ReadAllTextAsync(FilePath, Encoding.UTF8);
			var config = JsonConvert.DeserializeObject<Config>(jsonString);
			JsonConvert.PopulateObject(jsonString, this);
			// Just debug convenience.
			_backendUri = GetCurrentBackendUri();
		}

		public void CopyFrom(Config config)
		{
			var configSerialized = JsonConvert.SerializeObject(config);
			JsonConvert.PopulateObject(configSerialized, this);
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
			var newConfig = JsonConvert.DeserializeObject<Config>(jsonString);
			return !AreDeepEqual(newConfig);
		}

		public bool AreDeepEqual(Config otherConfig)
		{
			var currentConfig = JObject.FromObject(this);
			var otherConfigJson = JObject.FromObject(otherConfig);
			return JToken.DeepEquals(otherConfigJson, currentConfig);
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

		public TargetPrivacy GetTargetPrivacy()
		{
			if (MixUntilAnonymitySet == PrivacyLevelSome)
			{
				return TargetPrivacy.Some;
			}

			if (MixUntilAnonymitySet == PrivacyLevelFine)
			{
				return TargetPrivacy.Fine;
			}

			if (MixUntilAnonymitySet == PrivacyLevelStrong)
			{
				return TargetPrivacy.Strong;
			}
			//the levels changed in the config file, adjust
			if (MixUntilAnonymitySet < PrivacyLevelSome)
			{
				return TargetPrivacy.None; //choose the lower
			}

			if (MixUntilAnonymitySet < PrivacyLevelFine)
			{
				return TargetPrivacy.Some;
			}

			if (MixUntilAnonymitySet < PrivacyLevelStrong)
			{
				return TargetPrivacy.Fine;
			}

			if (MixUntilAnonymitySet > PrivacyLevelFine)
			{
				return TargetPrivacy.Strong;
			}

			return TargetPrivacy.None;
		}

		public int GetTargetLevel(TargetPrivacy target)
		{
			switch (target)
			{
				case TargetPrivacy.None:
					return 0;

				case TargetPrivacy.Some:
					return PrivacyLevelSome;

				case TargetPrivacy.Fine:
					return PrivacyLevelFine;

				case TargetPrivacy.Strong:
					return PrivacyLevelStrong;
			}
			return 0;
		}
	}
}
