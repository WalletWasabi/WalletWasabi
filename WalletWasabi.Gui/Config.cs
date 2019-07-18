using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
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
		/// <inheritdoc />
		public string FilePath { get; private set; }

		[JsonProperty(PropertyName = "Network")]
		[JsonConverter(typeof(NetworkJsonConverter))]
		public Network Network { get; internal set; }

		[JsonProperty(PropertyName = "MainNetBackendUriV3")]
		public string MainNetBackendUriV3 { get; private set; }

		[JsonProperty(PropertyName = "TestNetBackendUriV3")]
		public string TestNetBackendUriV3 { get; private set; }

		[JsonProperty(PropertyName = "MainNetFallbackBackendUri")]
		public string MainNetFallbackBackendUri { get; private set; }

		[JsonProperty(PropertyName = "TestNetFallbackBackendUri")]
		public string TestNetFallbackBackendUri { get; private set; }

		[JsonProperty(PropertyName = "RegTestBackendUriV3")]
		public string RegTestBackendUriV3 { get; private set; }

		[JsonProperty(PropertyName = "UseTor")]
		public bool? UseTor { get; internal set; }

		[JsonProperty(PropertyName = "TorSocks5EndPoint")]
		[JsonConverter(typeof(EndPointJsonConverter), Constants.DefaultTorSocksPort)]
		public EndPoint TorSocks5EndPoint { get; internal set; }

		[JsonProperty(PropertyName = "MainNetBitcoinP2pEndPoint")]
		[JsonConverter(typeof(EndPointJsonConverter), Constants.DefaultMainNetBitcoinP2pPort)]
		public EndPoint MainNetBitcoinP2pEndPoint { get; internal set; }

		[JsonProperty(PropertyName = "TestNetBitcoinP2pEndPoint")]
		[JsonConverter(typeof(EndPointJsonConverter), Constants.DefaultTestNetBitcoinP2pPort)]
		public EndPoint TestNetBitcoinP2pEndPoint { get; internal set; }

		[JsonProperty(PropertyName = "RegTestBitcoinP2pEndPoint")]
		[JsonConverter(typeof(EndPointJsonConverter), Constants.DefaultRegTestBitcoinP2pPort)]
		public EndPoint RegTestBitcoinP2pEndPoint { get; internal set; }

		[JsonProperty(PropertyName = "MixUntilAnonymitySet")]
		public int? MixUntilAnonymitySet
		{
			get => _mixUntilAnonymitySet;
			internal set
			{
				if (_mixUntilAnonymitySet != value)
				{
					_mixUntilAnonymitySet = value;
					if (value.HasValue && ServiceConfiguration != default)
					{
						ServiceConfiguration.MixUntilAnonymitySet = value.Value;
					}
				}
			}
		}

		[JsonProperty(PropertyName = "PrivacyLevelSome")]
		public int? PrivacyLevelSome
		{
			get => _privacyLevelSome;
			internal set
			{
				if (_privacyLevelSome != value)
				{
					_privacyLevelSome = value;
					if (value.HasValue && ServiceConfiguration != default)
					{
						ServiceConfiguration.PrivacyLevelSome = value.Value;
					}
				}
			}
		}

		[JsonProperty(PropertyName = "PrivacyLevelFine")]
		public int? PrivacyLevelFine
		{
			get => _privacyLevelFine;
			internal set
			{
				if (_privacyLevelFine != value)
				{
					_privacyLevelFine = value;
					if (value.HasValue && ServiceConfiguration != default)
					{
						ServiceConfiguration.PrivacyLevelFine = value.Value;
					}
				}
			}
		}

		[JsonProperty(PropertyName = "PrivacyLevelStrong")]
		public int? PrivacyLevelStrong
		{
			get => _privacyLevelStrong;
			internal set
			{
				if (_privacyLevelStrong != value)
				{
					_privacyLevelStrong = value;
					if (value.HasValue && ServiceConfiguration != default)
					{
						ServiceConfiguration.PrivacyLevelStrong = value.Value;
					}
				}
			}
		}

		[JsonProperty(PropertyName = "DustThreshold")]
		[JsonConverter(typeof(MoneyBtcJsonConverter))]
		public Money DustThreshold { get; internal set; }

		private Uri _backendUri;
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
		private int? _mixUntilAnonymitySet;
		private int? _privacyLevelSome;
		private int? _privacyLevelFine;
		private int? _privacyLevelStrong;
		private EndPoint _bitcoinP2pEndPoint;

		public IPEndPoint GetTorSocks5EndPoint()
		{
			if (_torSocks5EndPoint is null)
			{
				_torSocks5EndPoint = TorSocks5EndPoint as IPEndPoint;
			}

			return _torSocks5EndPoint;
		}

		public EndPoint GetBitcoinP2pEndPoint()
		{
			if (_bitcoinP2pEndPoint is null)
			{
				if (Network == Network.Main)
				{
					_bitcoinP2pEndPoint = MainNetBitcoinP2pEndPoint;
				}
				else if (Network == Network.TestNet)
				{
					_bitcoinP2pEndPoint = TestNetBitcoinP2pEndPoint;
				}
				else if (Network == Network.RegTest)
				{
					_bitcoinP2pEndPoint = RegTestBitcoinP2pEndPoint;
				}
				else
				{
					throw new NotSupportedException("Network not supported.");
				}
			}

			return _bitcoinP2pEndPoint;
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

			Network = Network.Main;

			MainNetBackendUriV3 = "http://wasabiukrxmkdgve5kynjztuovbg43uxcbcxn6y2okcrsg7gb6jdmbad.onion/";
			TestNetBackendUriV3 = "http://testwnp3fugjln6vh5vpj7mvq3lkqqwjj3c2aafyu7laxz42kgwh2rad.onion/";
			MainNetFallbackBackendUri = "https://wasabiwallet.io/";
			TestNetFallbackBackendUri = "https://wasabiwallet.co/";
			RegTestBackendUriV3 = "http://localhost:37127/";

			UseTor = true;
			TorSocks5EndPoint = new IPEndPoint(IPAddress.Loopback, Constants.DefaultTorSocksPort);

			MainNetBitcoinP2pEndPoint = new IPEndPoint(IPAddress.Loopback, Constants.DefaultMainNetBitcoinP2pPort);
			TestNetBitcoinP2pEndPoint = new IPEndPoint(IPAddress.Loopback, Constants.DefaultTestNetBitcoinP2pPort);
			RegTestBitcoinP2pEndPoint = new IPEndPoint(IPAddress.Loopback, Constants.DefaultRegTestBitcoinP2pPort);

			MixUntilAnonymitySet = 50;
			PrivacyLevelSome = 2;
			PrivacyLevelFine = 21;
			PrivacyLevelStrong = 50;
			DustThreshold = Money.Coins(0.0001m);

			if (!File.Exists(FilePath))
			{
				Logger.LogInfo<Config>($"{nameof(Config)} file did not exist. Created at path: `{FilePath}`.");
			}
			else
			{
				await LoadFileAsync();
			}

			ServiceConfiguration = new ServiceConfiguration(MixUntilAnonymitySet.Value, PrivacyLevelSome.Value, PrivacyLevelFine.Value, PrivacyLevelStrong.Value, GetBitcoinP2pEndPoint(), DustThreshold);

			// Just debug convenience.
			_backendUri = GetCurrentBackendUri();

			await ToFileAsync();
		}

		public async Task LoadFileAsync()
		{
			string jsonString = await File.ReadAllTextAsync(FilePath, Encoding.UTF8);

			var config = JsonConvert.DeserializeObject<Config>(jsonString);

			Network = config.Network ?? Network;

			MainNetBackendUriV3 = config.MainNetBackendUriV3 ?? MainNetBackendUriV3;
			TestNetBackendUriV3 = config.TestNetBackendUriV3 ?? TestNetBackendUriV3;
			MainNetFallbackBackendUri = config.MainNetFallbackBackendUri ?? MainNetFallbackBackendUri;
			TestNetFallbackBackendUri = config.TestNetFallbackBackendUri ?? TestNetFallbackBackendUri;
			RegTestBackendUriV3 = config.RegTestBackendUriV3 ?? RegTestBackendUriV3;

			UseTor = config.UseTor ?? UseTor;
			TorSocks5EndPoint = config.TorSocks5EndPoint ?? TorSocks5EndPoint;

			MainNetBitcoinP2pEndPoint = config.MainNetBitcoinP2pEndPoint ?? MainNetBitcoinP2pEndPoint;
			TestNetBitcoinP2pEndPoint = config.TestNetBitcoinP2pEndPoint ?? TestNetBitcoinP2pEndPoint;
			RegTestBitcoinP2pEndPoint = config.RegTestBitcoinP2pEndPoint ?? RegTestBitcoinP2pEndPoint;

			MixUntilAnonymitySet = config.MixUntilAnonymitySet ?? MixUntilAnonymitySet;
			PrivacyLevelSome = config.PrivacyLevelSome ?? PrivacyLevelSome;
			PrivacyLevelFine = config.PrivacyLevelFine ?? PrivacyLevelFine;
			PrivacyLevelStrong = config.PrivacyLevelStrong ?? PrivacyLevelStrong;

			DustThreshold = config.DustThreshold ?? DustThreshold;

			ServiceConfiguration = config.ServiceConfiguration ?? ServiceConfiguration;

			// Just debug convenience.
			_backendUri = GetCurrentBackendUri();

			if (TryEnsureBackwardsCompatibility(jsonString))
			{
				await ToFileAsync();
			}
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
					return PrivacyLevelSome.Value;

				case TargetPrivacy.Fine:
					return PrivacyLevelFine.Value;

				case TargetPrivacy.Strong:
					return PrivacyLevelStrong.Value;
			}
			return 0;
		}

		private bool TryEnsureBackwardsCompatibility(string jsonString)
		{
			var jsObject = JsonConvert.DeserializeObject<JObject>(jsonString);
			bool saveIt = false;

			if (jsObject.TryGetValue("TorHost", out JToken jTorHost))
			{
				int port = Constants.DefaultTorSocksPort;
				if (jsObject.TryGetValue("TorSocks5Port", out JToken jTorSocks5Port) && int.TryParse(jTorSocks5Port.ToString(), out int p))
				{
					port = p;
				}

				if (EndPointParser.TryParse(jTorHost.ToString(), port, out EndPoint ep))
				{
					TorSocks5EndPoint = ep;
					saveIt = true;
				}
			}

			if (jsObject.TryGetValue("MainNetBitcoinCoreHost", out JToken jMainNetBitcoinCoreHost))
			{
				int port = Constants.DefaultMainNetBitcoinP2pPort;
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
				int port = Constants.DefaultTestNetBitcoinP2pPort;
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
				int port = Constants.DefaultRegTestBitcoinP2pPort;
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
