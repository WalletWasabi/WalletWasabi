using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Bases;
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
	public class Config : ConfigBase
	{
		public const int DefaultPrivacyLevelSome = 2;
		public const int DefaultPrivacyLevelFine = 21;
		public const int DefaultPrivacyLevelStrong = 50;
		public const int DefaultMixUntilAnonymitySet = 50;
		public const int DefaultTorSock5Port = 9050;
		public static readonly Money DefaultDustThreshold = Money.Coins(0.0001m);

		[JsonProperty(PropertyName = "Network")]
		[JsonConverter(typeof(NetworkJsonConverter))]
		public Network Network { get; internal set; } = Network.Main;

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

		[JsonProperty(PropertyName = "TorSocks5EndPoint")]
		[JsonConverter(typeof(EndPointJsonConverter), Constants.DefaultTorSocksPort)]
		public EndPoint TorSocks5EndPoint { get; internal set; } = new IPEndPoint(IPAddress.Loopback, Constants.DefaultTorSocksPort);

		[JsonProperty(PropertyName = "MainNetBitcoinP2pEndPoint")]
		[JsonConverter(typeof(EndPointJsonConverter), Constants.DefaultMainNetBitcoinP2pPort)]
		public EndPoint MainNetBitcoinP2pEndPoint { get; internal set; } = new IPEndPoint(IPAddress.Loopback, Constants.DefaultMainNetBitcoinP2pPort);

		[JsonProperty(PropertyName = "TestNetBitcoinP2pEndPoint")]
		[JsonConverter(typeof(EndPointJsonConverter), Constants.DefaultTestNetBitcoinP2pPort)]
		public EndPoint TestNetBitcoinP2pEndPoint { get; internal set; } = new IPEndPoint(IPAddress.Loopback, Constants.DefaultTestNetBitcoinP2pPort);

		[JsonProperty(PropertyName = "RegTestBitcoinP2pEndPoint")]
		[JsonConverter(typeof(EndPointJsonConverter), Constants.DefaultRegTestBitcoinP2pPort)]
		public EndPoint RegTestBitcoinP2pEndPoint { get; internal set; } = new IPEndPoint(IPAddress.Loopback, Constants.DefaultRegTestBitcoinP2pPort);

		[DefaultValue(DefaultMixUntilAnonymitySet)]
		[JsonProperty(PropertyName = "MixUntilAnonymitySet", DefaultValueHandling = DefaultValueHandling.Populate)]
		public int MixUntilAnonymitySet
		{
			get => _mixUntilAnonymitySet;
			internal set
			{
				if (_mixUntilAnonymitySet != value)
				{
					_mixUntilAnonymitySet = value;
					if (ServiceConfiguration != default)
					{
						ServiceConfiguration.MixUntilAnonymitySet = value;
					}
				}
			}
		}

		[DefaultValue(DefaultPrivacyLevelSome)]
		[JsonProperty(PropertyName = "PrivacyLevelSome", DefaultValueHandling = DefaultValueHandling.Populate)]
		public int PrivacyLevelSome
		{
			get => _privacyLevelSome;
			internal set
			{
				if (_privacyLevelSome != value)
				{
					_privacyLevelSome = value;
					if (ServiceConfiguration != default)
					{
						ServiceConfiguration.PrivacyLevelSome = value;
					}
				}
			}
		}

		[DefaultValue(DefaultPrivacyLevelFine)]
		[JsonProperty(PropertyName = "PrivacyLevelFine", DefaultValueHandling = DefaultValueHandling.Populate)]
		public int PrivacyLevelFine
		{
			get => _privacyLevelFine;
			internal set
			{
				if (_privacyLevelFine != value)
				{
					_privacyLevelFine = value;
					if (ServiceConfiguration != default)
					{
						ServiceConfiguration.PrivacyLevelFine = value;
					}
				}
			}
		}

		[DefaultValue(DefaultPrivacyLevelStrong)]
		[JsonProperty(PropertyName = "PrivacyLevelStrong", DefaultValueHandling = DefaultValueHandling.Populate)]
		public int PrivacyLevelStrong
		{
			get => _privacyLevelStrong;
			internal set
			{
				if (_privacyLevelStrong != value)
				{
					_privacyLevelStrong = value;
					if (ServiceConfiguration != default)
					{
						ServiceConfiguration.PrivacyLevelStrong = value;
					}
				}
			}
		}

		[JsonProperty(PropertyName = "DustThreshold")]
		[JsonConverter(typeof(MoneyBtcJsonConverter))]
		public Money DustThreshold { get; internal set; } = DefaultDustThreshold;

		private Uri _backendUri = null;
		private Uri _fallbackBackendUri;

		public ServiceConfiguration ServiceConfiguration { get; private set; }

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
			else if (Network == Network.RegTest)
			{
				_backendUri = new Uri(RegTestBackendUriV3);
			}
			else
			{
				throw new NotSupportedException($"{nameof(Network)} not supported: {Network}.");
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
			else if (Network == Network.RegTest)
			{
				_fallbackBackendUri = new Uri(RegTestBackendUriV3);
			}
			else
			{
				throw new NotSupportedException($"{nameof(Network)} not supported: {Network}.");
			}

			return _fallbackBackendUri;
		}

		private int _mixUntilAnonymitySet;
		private int _privacyLevelSome;
		private int _privacyLevelFine;
		private int _privacyLevelStrong;

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

		public Config() : base()
		{
		}

		public Config(string filePath) : base(filePath)
		{
		}

		/// <inheritdoc />
		public override async Task LoadFileAsync()
		{
			await base.LoadFileAsync();

			ServiceConfiguration = new ServiceConfiguration(MixUntilAnonymitySet, PrivacyLevelSome, PrivacyLevelFine, PrivacyLevelStrong, GetBitcoinP2pEndPoint(), DustThreshold);

			// Just debug convenience.
			_backendUri = GetCurrentBackendUri();
		}

		public void SetP2PEndpoint(EndPoint endPoint)
		{
			switch (Network.Name)
			{
				case nameof(Network.Main):
					MainNetBitcoinP2pEndPoint = endPoint;
					break;

				case nameof(Network.TestNet):
					TestNetBitcoinP2pEndPoint = endPoint;
					break;

				case nameof(Network.RegTest):
					RegTestBitcoinP2pEndPoint = endPoint;
					break;

				default:
					throw new NotSupportedException("Unsupported network");
			}
		}

		public EndPoint GetP2PEndpoint()
		{
			switch (Network.Name)
			{
				case nameof(Network.Main):
					return MainNetBitcoinP2pEndPoint;

				case nameof(Network.TestNet):
					return TestNetBitcoinP2pEndPoint;

				case nameof(Network.RegTest):
					return RegTestBitcoinP2pEndPoint;

				default:
					throw new NotSupportedException("Unsupported network");
			}
		}

		public static async Task<Config> LoadOrCreateDefaultFileAsync(string path)
		{
			var config = new Config(path);
			await config.LoadOrCreateDefaultFileAsync();
			return config;
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

		protected override bool TryEnsureBackwardsCompatibility(string jsonString)
		{
			try
			{
				var jsObject = JsonConvert.DeserializeObject<JObject>(jsonString);
				bool saveIt = false;

				var torHost = jsObject.Value<string>("TorHost");
				var torSocks5Port = jsObject.Value<int?>("TorSocks5Port");
				var mainNetBitcoinCoreHost = jsObject.Value<string>("MainNetBitcoinCoreHost");
				var mainNetBitcoinCorePort = jsObject.Value<int?>("MainNetBitcoinCorePort");
				var testNetBitcoinCoreHost = jsObject.Value<string>("TestNetBitcoinCoreHost");
				var testNetBitcoinCorePort = jsObject.Value<int?>("TestNetBitcoinCorePort");
				var regTestBitcoinCoreHost = jsObject.Value<string>("RegTestBitcoinCoreHost");
				var regTestBitcoinCorePort = jsObject.Value<int?>("RegTestBitcoinCorePort");

				if (torHost != null)
				{
					int port = torSocks5Port ?? Constants.DefaultTorSocksPort;

					if (EndPointParser.TryParse(torHost, port, out EndPoint ep))
					{
						TorSocks5EndPoint = ep;
						saveIt = true;
					}
				}

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
