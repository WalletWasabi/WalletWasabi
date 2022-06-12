using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.ComponentModel;
using System.Net;
using WalletWasabi.Bases;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;
using WalletWasabi.JsonConverters;
using WalletWasabi.JsonConverters.Bitcoin;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Tor;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Fluent;

[JsonObject(MemberSerialization.OptIn)]
public class Config : ConfigBase
{
	public const int DefaultJsonRpcServerPort = 37128;
	public static readonly Money DefaultDustThreshold = Money.Coins(Constants.DefaultDustThreshold);

	private Uri? _backendUri = null;
	private Uri? _fallbackBackendUri;

	/// <summary>
	/// Constructor for config population using Newtonsoft.JSON.
	/// </summary>
	public Config() : base()
	{
		ServiceConfiguration = null!;
	}

	public Config(string filePath) : base(filePath)
	{
		ServiceConfiguration = new ServiceConfiguration(GetBitcoinP2pEndPoint(), DustThreshold);
	}

	[JsonProperty(PropertyName = "Network")]
	[JsonConverter(typeof(NetworkJsonConverter))]
	public Network Network { get; internal set; } = Network.Main;

	[DefaultValue("http://wasabiukrxmkdgve5kynjztuovbg43uxcbcxn6y2okcrsg7gb6jdmbad.onion/")]
	[JsonProperty(PropertyName = "MainNetBackendUriV3", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string MainNetBackendUriV3 { get; private set; } = "http://wasabiukrxmkdgve5kynjztuovbg43uxcbcxn6y2okcrsg7gb6jdmbad.onion/";

	[DefaultValue("http://testwnp3fugjln6vh5vpj7mvq3lkqqwjj3c2aafyu7laxz42kgwh2rad.onion/")]
	[JsonProperty(PropertyName = "TestNetBackendUriV3", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string TestNetBackendUriV3 { get; private set; } = "http://testwnp3fugjln6vh5vpj7mvq3lkqqwjj3c2aafyu7laxz42kgwh2rad.onion/";

	[DefaultValue("https://wasabiwallet.io/")]
	[JsonProperty(PropertyName = "MainNetFallbackBackendUri", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string MainNetFallbackBackendUri { get; private set; } = "https://wasabiwallet.io/";

	[DefaultValue("https://wasabiwallet.co/")]
	[JsonProperty(PropertyName = "TestNetFallbackBackendUri", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string TestNetFallbackBackendUri { get; private set; } = "https://wasabiwallet.co/";

	[DefaultValue("http://localhost:37127/")]
	[JsonProperty(PropertyName = "RegTestBackendUriV3", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string RegTestBackendUriV3 { get; private set; } = "http://localhost:37127/";

	[DefaultValue(true)]
	[JsonProperty(PropertyName = "UseTor", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool UseTor { get; internal set; } = true;

	[DefaultValue(false)]
	[JsonProperty(PropertyName = "TerminateTorOnExit", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool TerminateTorOnExit { get; internal set; } = false;

	[DefaultValue(false)]
	[JsonProperty(PropertyName = "StartLocalBitcoinCoreOnStartup", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool StartLocalBitcoinCoreOnStartup { get; internal set; } = false;

	[DefaultValue(true)]
	[JsonProperty(PropertyName = "StopLocalBitcoinCoreOnShutdown", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool StopLocalBitcoinCoreOnShutdown { get; internal set; } = true;

	[JsonProperty(PropertyName = "LocalBitcoinCoreDataDir")]
	public string LocalBitcoinCoreDataDir { get; internal set; } = EnvironmentHelpers.GetDefaultBitcoinCoreDataDirOrEmptyString();

	[JsonProperty(PropertyName = "MainNetBitcoinP2pEndPoint")]
	[JsonConverter(typeof(EndPointJsonConverter), Constants.DefaultMainNetBitcoinP2pPort)]
	public EndPoint MainNetBitcoinP2pEndPoint { get; internal set; } = new IPEndPoint(IPAddress.Loopback, Constants.DefaultMainNetBitcoinP2pPort);

	[JsonProperty(PropertyName = "TestNetBitcoinP2pEndPoint")]
	[JsonConverter(typeof(EndPointJsonConverter), Constants.DefaultTestNetBitcoinP2pPort)]
	public EndPoint TestNetBitcoinP2pEndPoint { get; internal set; } = new IPEndPoint(IPAddress.Loopback, Constants.DefaultTestNetBitcoinP2pPort);

	[JsonProperty(PropertyName = "RegTestBitcoinP2pEndPoint")]
	[JsonConverter(typeof(EndPointJsonConverter), Constants.DefaultRegTestBitcoinP2pPort)]
	public EndPoint RegTestBitcoinP2pEndPoint { get; internal set; } = new IPEndPoint(IPAddress.Loopback, Constants.DefaultRegTestBitcoinP2pPort);

	[DefaultValue(false)]
	[JsonProperty(PropertyName = "JsonRpcServerEnabled", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool JsonRpcServerEnabled { get; internal set; }

	[DefaultValue("")]
	[JsonProperty(PropertyName = "JsonRpcUser", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string JsonRpcUser { get; internal set; } = "";

	[DefaultValue("")]
	[JsonProperty(PropertyName = "JsonRpcPassword", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string JsonRpcPassword { get; internal set; } = "";

	[JsonProperty(PropertyName = "JsonRpcServerPrefixes")]
	public string[] JsonRpcServerPrefixes { get; internal set; } = new[]
	{
			"http://127.0.0.1:37128/",
			"http://localhost:37128/"
		};

	[JsonProperty(PropertyName = "DustThreshold")]
	[JsonConverter(typeof(MoneyBtcJsonConverter))]
	public Money DustThreshold { get; internal set; } = DefaultDustThreshold;

	public ServiceConfiguration ServiceConfiguration { get; private set; }

	public Uri GetCurrentBackendUri()
	{
		if (_backendUri is { })
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
			throw new NotSupportedNetworkException(Network);
		}

		return _backendUri;
	}

	public Uri GetFallbackBackendUri()
	{
		if (_fallbackBackendUri is { })
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
			throw new NotSupportedNetworkException(Network);
		}

		return _fallbackBackendUri;
	}

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
			throw new NotSupportedNetworkException(Network);
		}
	}

	public void SetBitcoinP2pEndpoint(EndPoint endPoint)
	{
		if (Network == Network.Main)
		{
			MainNetBitcoinP2pEndPoint = endPoint;
		}
		else if (Network == Network.TestNet)
		{
			TestNetBitcoinP2pEndPoint = endPoint;
		}
		else if (Network == Network.RegTest)
		{
			RegTestBitcoinP2pEndPoint = endPoint;
		}
		else
		{
			throw new NotSupportedNetworkException(Network);
		}
	}

	/// <inheritdoc />
	public override void LoadFile()
	{
		base.LoadFile();

		ServiceConfiguration = new ServiceConfiguration(GetBitcoinP2pEndPoint(), DustThreshold);

		// Just debug convenience.
		_backendUri = GetCurrentBackendUri();
	}

	protected override bool TryEnsureBackwardsCompatibility(string jsonString)
	{
		try
		{
			var jsObject = JsonConvert.DeserializeObject<JObject>(jsonString);

			if (jsObject is null)
			{
				Logger.LogWarning("Failed to parse config JSON.");
				return false;
			}

			bool saveIt = false;

			var torHost = jsObject.Value<string>("TorHost");
			var torSocks5Port = jsObject.Value<int?>("TorSocks5Port");
			var mainNetBitcoinCoreHost = jsObject.Value<string>("MainNetBitcoinCoreHost");
			var mainNetBitcoinCorePort = jsObject.Value<int?>("MainNetBitcoinCorePort");
			var testNetBitcoinCoreHost = jsObject.Value<string>("TestNetBitcoinCoreHost");
			var testNetBitcoinCorePort = jsObject.Value<int?>("TestNetBitcoinCorePort");
			var regTestBitcoinCoreHost = jsObject.Value<string>("RegTestBitcoinCoreHost");
			var regTestBitcoinCorePort = jsObject.Value<int?>("RegTestBitcoinCorePort");

			if (mainNetBitcoinCoreHost is { })
			{
				int port = mainNetBitcoinCorePort ?? Constants.DefaultMainNetBitcoinP2pPort;

				if (EndPointParser.TryParse(mainNetBitcoinCoreHost, port, out EndPoint? ep))
				{
					MainNetBitcoinP2pEndPoint = ep;
					saveIt = true;
				}
			}

			if (testNetBitcoinCoreHost is { })
			{
				int port = testNetBitcoinCorePort ?? Constants.DefaultTestNetBitcoinP2pPort;

				if (EndPointParser.TryParse(testNetBitcoinCoreHost, port, out EndPoint? ep))
				{
					TestNetBitcoinP2pEndPoint = ep;
					saveIt = true;
				}
			}

			if (regTestBitcoinCoreHost is { })
			{
				int port = regTestBitcoinCorePort ?? Constants.DefaultRegTestBitcoinP2pPort;

				if (EndPointParser.TryParse(regTestBitcoinCoreHost, port, out EndPoint? ep))
				{
					RegTestBitcoinP2pEndPoint = ep;
					saveIt = true;
				}
			}

			return saveIt;
		}
		catch (Exception ex)
		{
			Logger.LogWarning("Backwards compatibility couldn't be ensured.");
			Logger.LogInfo(ex);
			return false;
		}
	}
}
