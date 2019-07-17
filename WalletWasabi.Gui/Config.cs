using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
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

		[JsonProperty(PropertyName = "TorHost")]
		public string TorHost { get; internal set; }

		[JsonProperty(PropertyName = "TorSocks5Port")]
		public int? TorSocks5Port { get; internal set; }

		[JsonProperty(PropertyName = "MainNetBitcoinCoreHost")]
		public string MainNetBitcoinCoreHost { get; internal set; }

		[JsonProperty(PropertyName = "TestNetBitcoinCoreHost")]
		public string TestNetBitcoinCoreHost { get; internal set; }

		[JsonProperty(PropertyName = "RegTestBitcoinCoreHost")]
		public string RegTestBitcoinCoreHost { get; internal set; }

		[Obsolete()]
		[JsonProperty(PropertyName = "MainNetBitcoinCorePort", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public int? MainNetBitcoinCorePort { get; internal set; }

		[Obsolete()]
		[JsonProperty(PropertyName = "TestNetBitcoinCorePort", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public int? TestNetBitcoinCorePort { get; internal set; }

		[Obsolete()]
		[JsonProperty(PropertyName = "RegTestBitcoinCorePort", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public int? RegTestBitcoinCorePort { get; internal set; }

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
		private EndPoint _bitcoinCoreEndPoint;

		public IPEndPoint GetTorSocks5EndPoint()
		{
			if (_torSocks5EndPoint is null)
			{
				var host = IPAddress.Parse(TorHost);
				_torSocks5EndPoint = new IPEndPoint(host, (int)TorSocks5Port);
			}

			return _torSocks5EndPoint;
		}

		public EndPoint GetBitcoinCoreEndPoint()
		{
			if (_bitcoinCoreEndPoint is null)
			{
				string dnsHost = null;
				if (Network == Network.Main)
				{
					dnsHost = MainNetBitcoinCoreHost;
				}
				else if (Network == Network.TestNet)
				{
					dnsHost = TestNetBitcoinCoreHost;
				}
				else // if (Network == Network.RegTest)
				{
					dnsHost = RegTestBitcoinCoreHost;
				}

				if (TryParseEndpoint(dnsHost, Network.DefaultPort, out var endpoint))
				{
					_bitcoinCoreEndPoint = endpoint;
				}
				else
				{
					_bitcoinCoreEndPoint = new IPEndPoint(IPAddress.Loopback, Network.DefaultPort);
				}
			}

			return _bitcoinCoreEndPoint;
		}

		public static bool TryParseEndpoint(string str, int defaultPort, out EndPoint endPoint)
		{
			if (str == null)
			{
				throw new ArgumentNullException(nameof(str));
			}
			str = str.Trim();
			if (!NBitcoin.Utils.TryParseEndpoint(str, defaultPort, out endPoint))
			{
				if (!Uri.TryCreate(str, UriKind.Absolute, out var uri) || uri.HostNameType == UriHostNameType.Unknown)
				{
					return false;
				}
				if (!uri.Scheme.Equals("bitcoin-p2p", StringComparison.OrdinalIgnoreCase) &&
					!uri.Scheme.Equals("tcp", StringComparison.OrdinalIgnoreCase))
				{
					return false;
				}
				str = uri.Authority;
				if (!NBitcoin.Utils.TryParseEndpoint(str, defaultPort, out endPoint))
				{
					return false;
				}
			}

			if (endPoint.IsTor() && endPoint is DnsEndPoint dnsEndPoint)
			{
				try
				{
					NBitcoin.DataEncoders.Encoders.Base32.DecodeData(dnsEndPoint.Host.Remove(dnsEndPoint.Host.Length - ".onion".Length));
				}
				catch
				{
					endPoint = null;
					return false;
				}
			}

			if (endPoint is IPEndPoint ip &&
					ip.Address.AddressFamily == AddressFamily.InterNetworkV6 &&
					!endPoint.IsTor() && // Onioncat address would work even if the OS does not support IPv6
					!Socket.OSSupportsIPv6)
			{
				endPoint = null;
				return false;
			}

			return true;
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
			TorHost = IPAddress.Loopback.ToString();
			TorSocks5Port = 9050;

			MainNetBitcoinCoreHost = IPAddress.Loopback.ToString();
			TestNetBitcoinCoreHost = IPAddress.Loopback.ToString();
			RegTestBitcoinCoreHost = IPAddress.Loopback.ToString();

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

			ServiceConfiguration = new ServiceConfiguration(MixUntilAnonymitySet.Value, PrivacyLevelSome.Value, PrivacyLevelFine.Value, PrivacyLevelStrong.Value, GetBitcoinCoreEndPoint(), DustThreshold);

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
			TorHost = config.TorHost ?? TorHost;
			TorSocks5Port = config.TorSocks5Port ?? TorSocks5Port;
#pragma warning disable CS0612 // Type or member is obsolete
			MigrateBackwardCompatiblePortField(() => config.MainNetBitcoinCorePort,
												v => config.MainNetBitcoinCorePort = v,
												() => config.MainNetBitcoinCoreHost,
												v => config.MainNetBitcoinCoreHost = v,
												Network.Main);
			MigrateBackwardCompatiblePortField(() => config.TestNetBitcoinCorePort,
												v => config.TestNetBitcoinCorePort = v,
												() => config.TestNetBitcoinCoreHost,
												v => config.TestNetBitcoinCoreHost = v,
												Network.TestNet);
			MigrateBackwardCompatiblePortField(() => config.RegTestBitcoinCorePort,
												v => config.RegTestBitcoinCorePort = v,
												() => config.RegTestBitcoinCoreHost,
												v => config.RegTestBitcoinCoreHost = v,
												Network.RegTest);
#pragma warning restore CS0612 // Type or member is obsolete
			MainNetBitcoinCoreHost = config.MainNetBitcoinCoreHost ?? MainNetBitcoinCoreHost;
			TestNetBitcoinCoreHost = config.TestNetBitcoinCoreHost ?? TestNetBitcoinCoreHost;
			RegTestBitcoinCoreHost = config.RegTestBitcoinCoreHost ?? RegTestBitcoinCoreHost;

			MixUntilAnonymitySet = config.MixUntilAnonymitySet ?? MixUntilAnonymitySet;
			PrivacyLevelSome = config.PrivacyLevelSome ?? PrivacyLevelSome;
			PrivacyLevelFine = config.PrivacyLevelFine ?? PrivacyLevelFine;
			PrivacyLevelStrong = config.PrivacyLevelStrong ?? PrivacyLevelStrong;

			DustThreshold = config.DustThreshold ?? DustThreshold;

			ServiceConfiguration = config.ServiceConfiguration ?? ServiceConfiguration;

			// Just debug convenience.
			_backendUri = GetCurrentBackendUri();
		}

		void MigrateBackwardCompatiblePortField(Func<int?> getPort, Action<int> setPort, Func<string> getHost, Action<string> setHost, Network network)
		{
			if (getPort() is int port)
			{
				var newHost = $"{getHost()}:{port}";
				if (TryParseEndpoint(newHost, network.DefaultPort, out _))
				{
					setHost(newHost);
				}
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
	}
}
