using NBitcoin;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;
using Org.BouncyCastle.Math;
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

namespace WalletWasabi.Gui
{
	[JsonObject(MemberSerialization.OptIn)]
	public class Config : IConfig
	{
		/// <inheritdoc />
		public string FilePath { get; private set; }

		[JsonProperty(PropertyName = "Network")]
		[JsonConverter(typeof(NetworkJsonConverter))]
		public Network Network { get; private set; }

		[JsonProperty(PropertyName = "MainNetBackendUri")]
		public string MainNetBackendUri { get; private set; }

		[JsonProperty(PropertyName = "TestNetBackendUri")]
		public string TestNetBackendUri { get; private set; }

		[JsonProperty(PropertyName = "RegTestBackendUri")]
		public string RegTestBackendUri { get; private set; }

		[JsonProperty(PropertyName = "TestNetBlindingRsaPubKey")]
		public string TestNetBlindingRsaPubKey { get; private set; }

		[JsonProperty(PropertyName = "MainNetBlindingRsaPubKey")]
		public string MainNetBlindingRsaPubKey { get; private set; }

		[JsonProperty(PropertyName = "RegTestBlindingRsaPubKey")]
		public string RegTestBlindingRsaPubKey { get; private set; }

		[JsonProperty(PropertyName = "TorSocks5IPAddress")]
		public string TorSocks5IPAddress { get; private set; }

		[JsonProperty(PropertyName = "TorSocks5Port")]
		public string TorSocks5Port { get; private set; }

		private Uri _backendUri;

		public Uri GetCurrentBackendUri()
		{
			if (_backendUri != null) return _backendUri;

			if (Network == Network.Main)
			{
				_backendUri = new Uri(MainNetBackendUri);
			}
			else if (Network == Network.TestNet)
			{
				_backendUri = new Uri(TestNetBackendUri);
			}
			else // RegTest
			{
				_backendUri = new Uri(RegTestBackendUri);
			}

			return _backendUri;
		}

		private IPEndPoint _torIpEndPoint;
		public IPEndPoint GetTorEndPoint() 
		{
			if (_torIpEndPoint != null) return _torIpEndPoint;
			if (TorSocks5Port == null && TorSocks5IPAddress == null) return null;

			var ipAddress = IPAddress.Parse(TorSocks5IPAddress ?? "127.0.0.1");
			var port = int.Parse(TorSocks5Port ?? "9050");

			return _torIpEndPoint = new IPEndPoint(ipAddress, port);
		}

		private BlindingRsaPubKey _blindingRsaPubKey;

		public BlindingRsaPubKey GetBlindingRsaPubKey()
		{
			if (_blindingRsaPubKey != null) return _blindingRsaPubKey;

			if (Network == Network.Main)
			{
				_blindingRsaPubKey = new BlindingRsaPubKey(new BigInteger(MainNetBlindingRsaPubKey), Constants.RsaPubKeyExponent);
			}
			else if (Network == Network.TestNet)
			{
				_blindingRsaPubKey = new BlindingRsaPubKey(new BigInteger(TestNetBlindingRsaPubKey), Constants.RsaPubKeyExponent);
			}
			else // RegTest
			{
				_blindingRsaPubKey = new BlindingRsaPubKey(new BigInteger(RegTestBlindingRsaPubKey), Constants.RsaPubKeyExponent);
			}

			return _blindingRsaPubKey;
		}

		public Config()
		{
			_backendUri = null;
			_blindingRsaPubKey = null;
		}

		public Config(string filePath)
		{
			_backendUri = null;
			_blindingRsaPubKey = null;
			SetFilePath(filePath);
		}

		public Config(Network network, string mainNetBackendUri, string testNetBackendUri, string regTestBackendUri, string mainNetBlindingRsaPubKey, string testNetBlindingRsaPubKey, string regTestBlindingRsaPubKey)
		{
			Network = Guard.NotNull(nameof(network), network);

			MainNetBackendUri = Guard.NotNullOrEmptyOrWhitespace(nameof(mainNetBackendUri), mainNetBackendUri);
			TestNetBackendUri = Guard.NotNullOrEmptyOrWhitespace(nameof(testNetBackendUri), testNetBackendUri);
			RegTestBackendUri = Guard.NotNullOrEmptyOrWhitespace(nameof(regTestBackendUri), regTestBackendUri);

			MainNetBlindingRsaPubKey = Guard.NotNullOrEmptyOrWhitespace(nameof(mainNetBlindingRsaPubKey), mainNetBlindingRsaPubKey);
			TestNetBlindingRsaPubKey = Guard.NotNullOrEmptyOrWhitespace(nameof(testNetBlindingRsaPubKey), testNetBlindingRsaPubKey);
			RegTestBlindingRsaPubKey = Guard.NotNullOrEmptyOrWhitespace(nameof(regTestBlindingRsaPubKey), regTestBlindingRsaPubKey);
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

		private void OverwriteWith(Config config) 
		{
			Network = config.Network ?? Network;

			MainNetBackendUri = config.MainNetBackendUri ?? MainNetBackendUri;
			TestNetBackendUri = config.TestNetBackendUri ?? TestNetBackendUri;
			RegTestBackendUri = config.RegTestBackendUri ?? RegTestBackendUri;

			MainNetBlindingRsaPubKey = config.MainNetBlindingRsaPubKey ?? MainNetBlindingRsaPubKey;
			TestNetBlindingRsaPubKey = config.TestNetBlindingRsaPubKey ?? TestNetBlindingRsaPubKey;
			RegTestBlindingRsaPubKey = config.RegTestBlindingRsaPubKey ?? RegTestBlindingRsaPubKey;

			TorSocks5IPAddress = config.TorSocks5IPAddress ?? TorSocks5IPAddress;
			TorSocks5Port = config.TorSocks5Port ?? TorSocks5Port;
		}

		/// <inheritdoc />
		public async Task LoadOrCreateDefaultFileAsync()
		{
			AssertFilePathSet();

			Network = Network.Main;

			MainNetBackendUri = "http://4jsmnfcsmbrlm7l7.onion/";
			TestNetBackendUri = "http://wtgjmaol3io5ijii.onion/";
			RegTestBackendUri = "http://localhost:37127/";

			MainNetBlindingRsaPubKey = "16421152619146079007287475569112871971988560541093277613438316709041030720662622782033859387192362542996510605015506477964793447620206674394713753349543444988246276357919473682408472170521463339860947351211455351029147665615454176157348164935212551240942809518428851690991984017733153078846480521091423447691527000770982623947706172997649440619968085147635776736938871139581019988225202983052255684151711253254086264386774936200194229277914886876824852466823571396538091430866082004097086602287294474304344865162932126041736158327600847754258634325228417149098062181558798532036659383679712667027126535424484318399849";
			TestNetBlindingRsaPubKey = "19473594448380717274202325076521698699373476167359253614775896809797414915031772455344343455269320444157176520539924715307970060890094127521516100754263825112231545354422893125394219335109864514907655429499954825469485252969706079992227103439161156022844535556626007277544637236136559868400854764962522288139619969507311597914908752685925185380735570791798593290356424409633800092336087046668579610273133131498947353719917407262847070395909920415822288443947309434039008038907229064999576278651443575362470457496666718250346530518268694562965606704838796709743032825816642704620776596590683042135764246115456630753521";
			RegTestBlindingRsaPubKey = "22150624048432351435695977813740447889408430038879549048669066759540857545194001686487035241226456922025362879904859086838539432404987971759281429087375036048566838323339034078875508311398019006566184621390613010655498049414411420453947773863327821032649547904953655351771067398194902527635974622680354037013275997209153454388073967935123137747633576410851133282514228950508503034222184195026309976327466634252381374641066331514368416311365206032260350939804067745887217463885470620044453632242044977087525656336957163920422847954554131015783995416461568282600638297091432144315385391445294118275302802043857482568817";

			if (!File.Exists(FilePath))
			{
				Logger.LogInfo<Config>($"{nameof(Config)} file did not exist. Created at path: `{FilePath}`.");
			}
			else
			{
				string jsonString = await File.ReadAllTextAsync(FilePath, Encoding.UTF8);
				OverwriteWith(JsonConvert.DeserializeObject<Config>(jsonString));
			}

			_backendUri = GetCurrentBackendUri();
			_blindingRsaPubKey = GetBlindingRsaPubKey();
			
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
			var config = JsonConvert.DeserializeObject<Config>(jsonString);

			if (Network != config.Network)
			{
				return true;
			}

			if (!TestNetBackendUri.Equals(config.TestNetBackendUri, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			if (!RegTestBackendUri.Equals(config.RegTestBackendUri, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			if (!MainNetBackendUri.Equals(config.MainNetBackendUri, StringComparison.OrdinalIgnoreCase))
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
			if (FilePath == null) throw new NotSupportedException($"{nameof(FilePath)} is not set. Use {nameof(SetFilePath)} to set it.");
		}
	}
}
