using NBitcoin;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Net;
using WalletWasabi.Bases;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;
using WalletWasabi.JsonConverters;
using WalletWasabi.JsonConverters.Bitcoin;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent;

[JsonObject(MemberSerialization.OptIn)]
public class Config : ConfigBase
{
	public const int DefaultJsonRpcServerPort = 37128;
	public static readonly Money DefaultDustThreshold = Money.Coins(Constants.DefaultDustThreshold);

	private Uri? _backendUri;
	private Uri? _coordinatorUri;

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

	[DefaultValue("https://wasabiwallet.io/")]
	[JsonProperty(PropertyName = "MainNetBackendUri", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string MainNetBackendUri { get; private set; } = "https://wasabiwallet.io/";

	[DefaultValue("https://wasabiwallet.co/")]
	[JsonProperty(PropertyName = "TestNetClearnetBackendUri", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string TestNetBackendUri { get; private set; } = "https://wasabiwallet.co/";

	[DefaultValue("http://localhost:37127/")]
	[JsonProperty(PropertyName = "RegTestBackendUri", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string RegTestBackendUri { get; private set; } = "http://localhost:37127/";

	[JsonProperty(PropertyName = "MainNetCoordinatorUri", DefaultValueHandling = DefaultValueHandling.Ignore)]
	public string? MainNetCoordinatorUri { get; private set; }

	[JsonProperty(PropertyName = "TestNetCoordinatorUri", DefaultValueHandling = DefaultValueHandling.Ignore)]
	public string? TestNetCoordinatorUri { get; private set; }

	[JsonProperty(PropertyName = "RegTestCoordinatorUri", DefaultValueHandling = DefaultValueHandling.Ignore)]
	public string? RegTestCoordinatorUri { get; private set; }

	[DefaultValue(true)]
	[JsonProperty(PropertyName = "UseTor", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool UseTor { get; internal set; } = true;

	[DefaultValue(false)]
	[JsonProperty(PropertyName = "TerminateTorOnExit", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool TerminateTorOnExit { get; internal set; } = false;

	[DefaultValue(true)]
	[JsonProperty(PropertyName = "DownloadNewVersion", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool DownloadNewVersion { get; internal set; } = true;

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

	[JsonProperty(PropertyName = "EnableGpu")]
	public bool EnableGpu { get; internal set; } = true;

	[DefaultValue("CoinJoinCoordinatorIdentifier")]
	[JsonProperty(PropertyName = "CoordinatorIdentifier", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string CoordinatorIdentifier { get; set; } = "CoinJoinCoordinatorIdentifier";

	public ServiceConfiguration ServiceConfiguration { get; private set; }

	public Uri GetBackendUri()
	{
		if (_backendUri is { })
		{
			return _backendUri;
		}

		if (Network == Network.Main)
		{
			_backendUri = new Uri(MainNetBackendUri);
		}
		else if (Network == Network.TestNet)
		{
			_backendUri = new Uri(TestNetBackendUri);
		}
		else if (Network == Network.RegTest)
		{
			_backendUri = new Uri(RegTestBackendUri);
		}
		else
		{
			throw new NotSupportedNetworkException(Network);
		}

		return _backendUri;
	}

	public Uri GetCoordinatorUri()
	{
		if (_coordinatorUri is { })
		{
			return _coordinatorUri;
		}

		var result = Network switch
		{
			{ } n when n == Network.Main => MainNetCoordinatorUri,
			{ } n when n == Network.TestNet => TestNetCoordinatorUri,
			{ } n when n == Network.RegTest => RegTestCoordinatorUri,
			_ => throw new NotSupportedNetworkException(Network)
		};

		_coordinatorUri = result is null ? GetBackendUri() : new Uri(result);
		return _coordinatorUri;
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

	/// <inheritdoc/>
	public override void LoadFile(bool createIfMissing = false)
	{
		base.LoadFile(createIfMissing);

		ServiceConfiguration = new ServiceConfiguration(GetBitcoinP2pEndPoint(), DustThreshold);
	}
}
