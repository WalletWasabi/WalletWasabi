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
		[JsonConverter(typeof(EndPointJsonConverter), Constants.DefaultMainNetBintcoinP2pPort)]
		public EndPoint MainNetBitcoinP2pEndPoint { get; internal set; }

		[JsonProperty(PropertyName = "TestNetBitcoinP2pEndPoint")]
		[JsonConverter(typeof(EndPointJsonConverter), Constants.DefaultTestNetBintcoinP2pPort)]
		public EndPoint TestNetBitcoinP2pEndPoint { get; internal set; }

		[JsonProperty(PropertyName = "RegTestBitcoinP2pEndPoint")]
		[JsonConverter(typeof(EndPointJsonConverter), Constants.DefaultRegTestBintcoinP2pPort)]
		public EndPoint RegTestBitcoinP2pEndPoint { get; internal set; }

		#region toDelete

		public string MainNetBitcoinCoreHost { get; internal set; }
		public int? MainNetBitcoinCorePort { get; internal set; }
		public string TestNetBitcoinCoreHost { get; internal set; }
		public int? TestNetBitcoinCorePort { get; internal set; }
		public string RegTestBitcoinCoreHost { get; internal set; }
		public int? RegTestBitcoinCorePort { get; internal set; }
		public string TorHost { get; internal set; }
		public int? TorSocks5Port { get; internal set; }

		#endregion toDelete

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
				if (TorSocks5EndPoint is IPEndPoint ipe)
				{
					_torSocks5EndPoint = ipe;
				}
				else
				{
					throw new InvalidCastException("Tor endPoint must be IPEndPoint");
				}
			}

			return _torSocks5EndPoint;
		}

		public EndPoint GetBitcoinCoreEndPoint()
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
				else // if (Network == Network.RegTest)
				{
					_bitcoinCoreEndPoint = RegTestBitcoinP2pEndPoint;
				}
			}

			return _bitcoinCoreEndPoint;
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

			TorSocks5EndPoint = StringToEndPoint("127.0.0.1", Constants.DefaultTorSocksPort); // TODO: Check if the port is OK

			MainNetBitcoinP2pEndPoint = StringToEndPoint("127.0.0.1", Constants.DefaultMainNetBintcoinP2pPort);
			TestNetBitcoinP2pEndPoint = StringToEndPoint("127.0.0.1", Constants.DefaultTestNetBintcoinP2pPort);
			RegTestBitcoinP2pEndPoint = StringToEndPoint("127.0.0.1", Constants.DefaultRegTestBintcoinP2pPort);

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

		private EndPoint StringToEndPoint(string endPointString, int port = 0)
		{
			if (IPAddress.TryParse(endPointString, out IPAddress ipAddress))
			{
				return new IPEndPoint(ipAddress, port);
			}

			return new DnsEndPoint(endPointString, port);
		}

		public async Task LoadFileAsync()
		{
			string jsonString = await File.ReadAllTextAsync(FilePath, Encoding.UTF8);

			var res = JsonConvert.DeserializeObject<JObject>(jsonString);
			bool saveIt = false;
			if (res.TryGetValue("TorHost", out JToken jTorHost))
			{
				int port = Constants.DefaultTorSocksPort; //TODO: check this if it is OK
				if (res.TryGetValue("TorSocks5Port", out JToken jTorSocks5Port))
				{
					port = int.Parse(jTorSocks5Port.ToString());
				}
				TorSocks5EndPoint = StringToEndPoint(jTorHost.ToString(), port);
				saveIt = true;
			}

			if (res.TryGetValue("MainNetBitcoinCoreHost", out JToken jMainNetBitcoinCoreHost))
			{
				int port = Constants.DefaultMainNetBintcoinP2pPort;
				if (res.TryGetValue("MainNetBitcoinCorePort", out JToken jMainNetBitcoinCorePort))
				{
					port = int.Parse(jMainNetBitcoinCorePort.ToString());
				}
				MainNetBitcoinP2pEndPoint = StringToEndPoint(jMainNetBitcoinCoreHost.ToString(), port);
				saveIt = true;
			}

			if (res.TryGetValue("TestNetBitcoinCoreHost", out JToken jTestNetBitcoinCoreHost))
			{
				int port = Constants.DefaultTestNetBintcoinP2pPort;
				if (res.TryGetValue("TestNetBitcoinCorePort", out JToken jTestNetBitcoinCorePort))
				{
					port = int.Parse(jTestNetBitcoinCorePort.ToString());
				}
				TestNetBitcoinP2pEndPoint = StringToEndPoint(jTestNetBitcoinCoreHost.ToString(), port);
				saveIt = true;
			}

			if (res.TryGetValue("RegTestBitcoinCoreHost", out JToken jRegTestBitcoinCoreHost))
			{
				int port = Constants.DefaultRegTestBintcoinP2pPort;
				if (res.TryGetValue("RegTestBitcoinCorePort", out JToken jRegTestBitcoinCorePort))
				{
					port = int.Parse(jRegTestBitcoinCorePort.ToString());
				}
				RegTestBitcoinP2pEndPoint = StringToEndPoint(jRegTestBitcoinCoreHost.ToString(), port);
				saveIt = true;

				if (saveIt)
				{
					await ToFileAsync();
					jsonString = await File.ReadAllTextAsync(FilePath, Encoding.UTF8);
				}

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
