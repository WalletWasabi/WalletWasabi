using NBitcoin;
using Newtonsoft.Json;
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

		[JsonProperty(PropertyName = "MainNetBitcoinCorePort")]
		public int? MainNetBitcoinCorePort { get; internal set; }

		[JsonProperty(PropertyName = "TestNetBitcoinCorePort")]
		public int? TestNetBitcoinCorePort { get; internal set; }

		[JsonProperty(PropertyName = "RegTestBitcoinCorePort")]
		public int? RegTestBitcoinCorePort { get; internal set; }

		[JsonProperty(PropertyName = "MainNetFetchFromLocalOnly")]
		public bool MainNetFetchFromLocalOnly { get; internal set; }

		[JsonProperty(PropertyName = "TestNetFetchFromLocalOnly")]
		public bool TestNetFetchFromLocalOnly { get; internal set; }

		[JsonProperty(PropertyName = "RegTestFetchFromLocalOnly")]
		public bool RegTestFetchFromLocalOnly { get; internal set; }

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

		public IPEndPoint GetTorSocks5EndPoint()
		{
			if (_torSocks5EndPoint is null)
			{
				var host = IPAddress.Parse(TorHost);
				_torSocks5EndPoint = new IPEndPoint(host, (int)TorSocks5Port);
			}

			return _torSocks5EndPoint;
		}

		public (EndPoint, bool) GetBitcoinCoreConnectivityParameters()
		{
			EndPoint bitcoinCoreEndPoint;
			IPAddress ipHost;
			string dnsHost = null;
			int? port = null;
			bool fetchFromLocalOnly;
			try
			{
				if (Network == Network.Main)
				{
					port = MainNetBitcoinCorePort;
					dnsHost = MainNetBitcoinCoreHost;
					ipHost = IPAddress.Parse(MainNetBitcoinCoreHost);
					fetchFromLocalOnly = MainNetFetchFromLocalOnly;
				}
				else if (Network == Network.TestNet)
				{
					port = TestNetBitcoinCorePort;
					dnsHost = TestNetBitcoinCoreHost;
					ipHost = IPAddress.Parse(TestNetBitcoinCoreHost);
					fetchFromLocalOnly = TestNetFetchFromLocalOnly;
				}
				else // if (Network == Network.RegTest)
				{
					port = RegTestBitcoinCorePort;
					dnsHost = RegTestBitcoinCoreHost;
					ipHost = IPAddress.Parse(RegTestBitcoinCoreHost);
					fetchFromLocalOnly = RegTestFetchFromLocalOnly;
				}

				bitcoinCoreEndPoint = new IPEndPoint(ipHost, port ?? Network.DefaultPort);
			}
			catch
			{
				bitcoinCoreEndPoint = new DnsEndPoint(dnsHost, port ?? Network.DefaultPort);
				fetchFromLocalOnly = false;
			}
			return (bitcoinCoreEndPoint, fetchFromLocalOnly);
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

		public Config(
			Network network,
			string mainNetBackendUriV3,
			string testNetBackendUriV3,
			string mainNetFallbackBackendUri,
			string testNetFallbackBackendUri,
			string regTestBackendUriV3,
			bool? useTor,
			string torHost,
			int? torSocks5Port,
			string mainNetBitcoinCoreHost,
			string testNetBitcoinCoreHost,
			string regTestBitcoinCoreHost,
			int? mainNetBitcoinCorePort,
			int? testNetBitcoinCorePort,
			int? regTestBitcoinCorePort,
			int? mixUntilAnonymitySet,
			int? privacyLevelSome,
			int? privacyLevelFine,
			int? privacyLevelStrong,
			Money dustThreshold)
		{
			Network = Guard.NotNull(nameof(network), network);

			MainNetBackendUriV3 = Guard.NotNullOrEmptyOrWhitespace(nameof(mainNetBackendUriV3), mainNetBackendUriV3);
			TestNetBackendUriV3 = Guard.NotNullOrEmptyOrWhitespace(nameof(testNetBackendUriV3), testNetBackendUriV3);
			MainNetFallbackBackendUri = Guard.NotNullOrEmptyOrWhitespace(nameof(mainNetFallbackBackendUri), mainNetFallbackBackendUri);
			TestNetFallbackBackendUri = Guard.NotNullOrEmptyOrWhitespace(nameof(testNetFallbackBackendUri), testNetFallbackBackendUri);
			TestNetBackendUriV3 = Guard.NotNullOrEmptyOrWhitespace(nameof(testNetBackendUriV3), testNetBackendUriV3);
			RegTestBackendUriV3 = Guard.NotNullOrEmptyOrWhitespace(nameof(regTestBackendUriV3), regTestBackendUriV3);

			UseTor = Guard.NotNull(nameof(useTor), useTor);
			TorHost = Guard.NotNullOrEmptyOrWhitespace(nameof(torHost), torHost);
			TorSocks5Port = Guard.NotNull(nameof(torSocks5Port), torSocks5Port);

			MainNetBitcoinCoreHost = Guard.NotNullOrEmptyOrWhitespace(nameof(mainNetBitcoinCoreHost), mainNetBitcoinCoreHost);
			TestNetBitcoinCoreHost = Guard.NotNullOrEmptyOrWhitespace(nameof(testNetBitcoinCoreHost), testNetBitcoinCoreHost);
			RegTestBitcoinCoreHost = Guard.NotNullOrEmptyOrWhitespace(nameof(regTestBitcoinCoreHost), regTestBitcoinCoreHost);
			MainNetBitcoinCorePort = Guard.NotNull(nameof(mainNetBitcoinCorePort), mainNetBitcoinCorePort);
			TestNetBitcoinCorePort = Guard.NotNull(nameof(testNetBitcoinCorePort), testNetBitcoinCorePort);
			RegTestBitcoinCorePort = Guard.NotNull(nameof(regTestBitcoinCorePort), regTestBitcoinCorePort);

			MixUntilAnonymitySet = Guard.NotNull(nameof(mixUntilAnonymitySet), mixUntilAnonymitySet);
			PrivacyLevelSome = Guard.NotNull(nameof(privacyLevelSome), privacyLevelSome);
			PrivacyLevelFine = Guard.NotNull(nameof(privacyLevelFine), privacyLevelFine);
			PrivacyLevelStrong = Guard.NotNull(nameof(privacyLevelStrong), privacyLevelStrong);

			DustThreshold = Guard.NotNull(nameof(dustThreshold), dustThreshold);

			var (coreEndPoint, fetchFromLocalOnly) = GetBitcoinCoreConnectivityParameters();
			ServiceConfiguration = new ServiceConfiguration(MixUntilAnonymitySet.Value, PrivacyLevelSome.Value, PrivacyLevelFine.Value, PrivacyLevelStrong.Value, coreEndPoint, fetchFromLocalOnly, DustThreshold);
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
			MainNetBitcoinCorePort = Network.Main.DefaultPort;
			TestNetBitcoinCorePort = Network.TestNet.DefaultPort;
			RegTestBitcoinCorePort = Network.RegTest.DefaultPort;
			MainNetFetchFromLocalOnly = false;
			TestNetFetchFromLocalOnly = false;
			RegTestFetchFromLocalOnly = false;

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

			var (coreEndPoint, fetchFromLocalOnly) = GetBitcoinCoreConnectivityParameters();
			ServiceConfiguration = new ServiceConfiguration(MixUntilAnonymitySet.Value, PrivacyLevelSome.Value, PrivacyLevelFine.Value, PrivacyLevelStrong.Value, coreEndPoint, fetchFromLocalOnly, DustThreshold);

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

			MainNetBitcoinCoreHost = config.MainNetBitcoinCoreHost ?? MainNetBitcoinCoreHost;
			TestNetBitcoinCoreHost = config.TestNetBitcoinCoreHost ?? TestNetBitcoinCoreHost;
			RegTestBitcoinCoreHost = config.RegTestBitcoinCoreHost ?? RegTestBitcoinCoreHost;
			MainNetBitcoinCorePort = config.MainNetBitcoinCorePort ?? MainNetBitcoinCorePort;
			TestNetBitcoinCorePort = config.TestNetBitcoinCorePort ?? TestNetBitcoinCorePort;
			RegTestBitcoinCorePort = config.RegTestBitcoinCorePort ?? RegTestBitcoinCorePort;
			MainNetFetchFromLocalOnly = config.MainNetFetchFromLocalOnly;
			TestNetFetchFromLocalOnly = config.TestNetFetchFromLocalOnly;
			RegTestFetchFromLocalOnly = config.RegTestFetchFromLocalOnly;

			MixUntilAnonymitySet = config.MixUntilAnonymitySet ?? MixUntilAnonymitySet;
			PrivacyLevelSome = config.PrivacyLevelSome ?? PrivacyLevelSome;
			PrivacyLevelFine = config.PrivacyLevelFine ?? PrivacyLevelFine;
			PrivacyLevelStrong = config.PrivacyLevelStrong ?? PrivacyLevelStrong;

			DustThreshold = config.DustThreshold ?? DustThreshold;

			ServiceConfiguration = config.ServiceConfiguration ?? ServiceConfiguration;

			// Just debug convenience.
			_backendUri = GetCurrentBackendUri();
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

			if (!MainNetBackendUriV3.Equals(config.MainNetBackendUriV3, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			if (!TestNetBackendUriV3.Equals(config.TestNetBackendUriV3, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			if (!MainNetFallbackBackendUri.Equals(config.MainNetFallbackBackendUri, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			if (!TestNetFallbackBackendUri.Equals(config.TestNetFallbackBackendUri, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			if (!RegTestBackendUriV3.Equals(config.RegTestBackendUriV3, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			if (MainNetFetchFromLocalOnly != config.MainNetFetchFromLocalOnly)
			{
					return true;
			}
			if (TestNetFetchFromLocalOnly != config.TestNetFetchFromLocalOnly)
			{
					return true;
			}
			if (RegTestFetchFromLocalOnly != config.RegTestFetchFromLocalOnly)
			{
					return true;
			}
			if (UseTor != config.UseTor)
			{
				return true;
			}

			if (!TorHost.Equals(config.TorHost, StringComparison.Ordinal))
			{
				return true;
			}
			if (TorSocks5Port != config.TorSocks5Port)
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

			if (MixUntilAnonymitySet != config.MixUntilAnonymitySet)
			{
				return true;
			}
			if (PrivacyLevelSome != config.PrivacyLevelSome)
			{
				return true;
			}
			if (PrivacyLevelFine != config.PrivacyLevelFine)
			{
				return true;
			}
			if (PrivacyLevelStrong != config.PrivacyLevelStrong)
			{
				return true;
			}

			if (DustThreshold != config.DustThreshold)
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
