using NBitcoin;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Net;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;
using WalletWasabi.JsonConverters;
using WalletWasabi.JsonConverters.Bitcoin;

namespace WalletWasabi.Daemon;

[JsonObject(MemberSerialization.OptIn)]
public class PersistentConfig
{
	public const int DefaultJsonRpcServerPort = 37128;
	public static readonly Money DefaultDustThreshold = Money.Coins(Constants.DefaultDustThreshold);

	/// <summary>
	/// Constructor for config population using Newtonsoft.JSON.
	/// </summary>
	[JsonConstructor]
	public PersistentConfig() : base()
	{
	}

	[JsonProperty(PropertyName = "Network")]
	[JsonConverter(typeof(NetworkJsonConverter))]
	public Network Network { get; set; } = Network.Main;

	[DefaultValue(Constants.BackendUri)]
	[JsonProperty(PropertyName = "MainNetBackendUri", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string MainNetBackendUri { get; private set; } = Constants.BackendUri;

	[DefaultValue(Constants.TestnetBackendUri)]
	[JsonProperty(PropertyName = "TestNetClearnetBackendUri", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string TestNetBackendUri { get; private set; } = Constants.TestnetBackendUri;

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
	public bool UseTor { get; set; } = true;

	[DefaultValue(false)]
	[JsonProperty(PropertyName = "TerminateTorOnExit", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool TerminateTorOnExit { get; set; } = false;

	[DefaultValue(true)]
	[JsonProperty(PropertyName = "DownloadNewVersion", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool DownloadNewVersion { get; set; } = true;

	[DefaultValue(false)]
	[JsonProperty(PropertyName = "StartLocalBitcoinCoreOnStartup", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool StartLocalBitcoinCoreOnStartup { get; set; } = false;

	[DefaultValue(true)]
	[JsonProperty(PropertyName = "StopLocalBitcoinCoreOnShutdown", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool StopLocalBitcoinCoreOnShutdown { get; set; } = true;

	[JsonProperty(PropertyName = "LocalBitcoinCoreDataDir")]
	public string LocalBitcoinCoreDataDir { get; set; } = EnvironmentHelpers.GetDefaultBitcoinCoreDataDirOrEmptyString();

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
	public Money DustThreshold { get; set; } = DefaultDustThreshold;

	[JsonProperty(PropertyName = "EnableGpu")]
	public bool EnableGpu { get; set; } = true;

	[DefaultValue("CoinJoinCoordinatorIdentifier")]
	[JsonProperty(PropertyName = "CoordinatorIdentifier", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string CoordinatorIdentifier { get; set; } = "CoinJoinCoordinatorIdentifier";

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

	public EndPoint GetBitcoinP2pEndPoint()
	{
		if (Network == Network.Main)
		{
			return MainNetBitcoinP2pEndPoint;
		}
		if (Network == Network.TestNet)
		{
			return TestNetBitcoinP2pEndPoint;
		}
		if (Network == Network.RegTest)
		{
			return RegTestBitcoinP2pEndPoint;
		}
		throw new NotSupportedNetworkException(Network);
	}

	public bool MigrateOldDefaultBackendUris()
	{
		bool hasChanged = false;

		if (MainNetBackendUri == "https://wasabiwallet.io/")
		{
			MainNetBackendUri = "https://api.wasabiwallet.io/";
			hasChanged = true;
		}

		if (TestNetBackendUri == "https://wasabiwallet.co/")
		{
			TestNetBackendUri = "https://api.wasabiwallet.co/";
			hasChanged = true;
		}

		return hasChanged;
	}
}
