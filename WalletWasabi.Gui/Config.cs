using NBitcoin;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using WalletWasabi.Crypto;
using WalletWasabi.Helpers;
using WalletWasabi.Interfaces;
using WalletWasabi.Logging;
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

		[JsonProperty(PropertyName = "TestNetBlindingRsaPubKey")]
		public string TestNetBlindingRsaPubKey { get; private set; }

		[JsonProperty(PropertyName = "MainNetBlindingRsaPubKey")]
		public string MainNetBlindingRsaPubKey { get; private set; }

		[JsonProperty(PropertyName = "RegTestBlindingRsaPubKey")]
		public string RegTestBlindingRsaPubKey { get; private set; }

		[JsonProperty(PropertyName = "TorHost")]
		public string TorHost { get; internal set; }

		[JsonProperty(PropertyName = "TorSocks5Port")]
		public int? TorSocks5Port { get; internal set; }

		private Uri _backendUri;
		private Uri _fallbackBackendUri;

		public Uri GetCurrentBackendUri()
		{
			if (TorProcessManager.RequestFallbackAddressUsage)
			{
				return GetFallbackBackendUri();
			}

			if (_backendUri != null) return _backendUri;

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
			if (_fallbackBackendUri != null) return _fallbackBackendUri;

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

		public Config()
		{
			_backendUri = null;
		}

		public Config(string filePath)
		{
			_backendUri = null;
			SetFilePath(filePath);
		}

		public Config(Network network, string mainNetBackendUriV3, string testNetBackendUriV3, string mainNetFallbackBackendUri, string testNetFallbackBackendUri, string regTestBackendUriV3, string mainNetBlindingRsaPubKey, string testNetBlindingRsaPubKey, string regTestBlindingRsaPubKey, string torHost, int? torSocks5Port)
		{
			Network = Guard.NotNull(nameof(network), network);

			MainNetBackendUriV3 = Guard.NotNullOrEmptyOrWhitespace(nameof(mainNetBackendUriV3), mainNetBackendUriV3);
			TestNetBackendUriV3 = Guard.NotNullOrEmptyOrWhitespace(nameof(testNetBackendUriV3), testNetBackendUriV3);
			MainNetFallbackBackendUri = Guard.NotNullOrEmptyOrWhitespace(nameof(mainNetFallbackBackendUri), mainNetFallbackBackendUri);
			TestNetFallbackBackendUri = Guard.NotNullOrEmptyOrWhitespace(nameof(testNetFallbackBackendUri), testNetFallbackBackendUri);
			TestNetBackendUriV3 = Guard.NotNullOrEmptyOrWhitespace(nameof(testNetBackendUriV3), testNetBackendUriV3);
			RegTestBackendUriV3 = Guard.NotNullOrEmptyOrWhitespace(nameof(regTestBackendUriV3), regTestBackendUriV3);

			MainNetBlindingRsaPubKey = Guard.NotNullOrEmptyOrWhitespace(nameof(mainNetBlindingRsaPubKey), mainNetBlindingRsaPubKey);
			TestNetBlindingRsaPubKey = Guard.NotNullOrEmptyOrWhitespace(nameof(testNetBlindingRsaPubKey), testNetBlindingRsaPubKey);
			RegTestBlindingRsaPubKey = Guard.NotNullOrEmptyOrWhitespace(nameof(regTestBlindingRsaPubKey), regTestBlindingRsaPubKey);

			TorHost = Guard.NotNullOrEmptyOrWhitespace(nameof(torHost), torHost);
			TorSocks5Port = Guard.NotNull(nameof(torSocks5Port), torSocks5Port);
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

			MainNetBlindingRsaPubKey = "16421152619146079007287475569112871971988560541093277613438316709041030720662622782033859387192362542996510605015506477964793447620206674394713753349543444988246276357919473682408472170521463339860947351211455351029147665615454176157348164935212551240942809518428851690991984017733153078846480521091423447691527000770982623947706172997649440619968085147635776736938871139581019988225202983052255684151711253254086264386774936200194229277914886876824852466823571396538091430866082004097086602287294474304344865162932126041736158327600847754258634325228417149098062181558798532036659383679712667027126535424484318399849";
			TestNetBlindingRsaPubKey = "19473594448380717274202325076521698699373476167359253614775896809797414915031772455344343455269320444157176520539924715307970060890094127521516100754263825112231545354422893125394219335109864514907655429499954825469485252969706079992227103439161156022844535556626007277544637236136559868400854764962522288139619969507311597914908752685925185380735570791798593290356424409633800092336087046668579610273133131498947353719917407262847070395909920415822288443947309434039008038907229064999576278651443575362470457496666718250346530518268694562965606704838796709743032825816642704620776596590683042135764246115456630753521";
			RegTestBlindingRsaPubKey = "19805113859916587596075691932680544502861190231482837135218424025384831779489269920459188761934430015720070888224509088334152377323792066863417578306968689084791111308220228369762317647065031287517847066636982918712457594977634751868801139928799770519498159408918552825154189662499542806401435306060574685644660620060856030258068774230785018597890946429085154652737881893280417266199217120928088976827238198687973416161592683348464391329470544325577003456519043206654114118700413398703915300437305079248875534495551617486735684899764754790953560278186552096336558799678522940277063802490218431509991711997155692507897";

			TorHost = IPAddress.Loopback.ToString();
			TorSocks5Port = 9050;

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

			MainNetBlindingRsaPubKey = config.MainNetBlindingRsaPubKey ?? MainNetBlindingRsaPubKey;
			TestNetBlindingRsaPubKey = config.TestNetBlindingRsaPubKey ?? TestNetBlindingRsaPubKey;
			RegTestBlindingRsaPubKey = config.RegTestBlindingRsaPubKey ?? RegTestBlindingRsaPubKey;

			TorHost = config.TorHost ?? TorHost;
			TorSocks5Port = config.TorSocks5Port ?? TorSocks5Port;

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

			if (!MainNetBlindingRsaPubKey.Equals(config.MainNetBlindingRsaPubKey, StringComparison.Ordinal))
			{
				return true;
			}

			if (!TestNetBlindingRsaPubKey.Equals(config.TestNetBlindingRsaPubKey, StringComparison.Ordinal))
			{
				return true;
			}

			if (!RegTestBlindingRsaPubKey.Equals(config.RegTestBlindingRsaPubKey, StringComparison.Ordinal))
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
