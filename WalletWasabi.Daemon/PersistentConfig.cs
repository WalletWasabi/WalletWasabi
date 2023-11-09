using NBitcoin;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;
using WalletWasabi.Interfaces;
using WalletWasabi.JsonConverters;
using WalletWasabi.JsonConverters.Bitcoin;

namespace WalletWasabi.Daemon;

[JsonObject(MemberSerialization.OptIn)]
public record PersistentConfig : IConfigNg
{
	public const int DefaultJsonRpcServerPort = 37128;
	public static readonly Money DefaultDustThreshold = Money.Coins(Constants.DefaultDustThreshold);

	[JsonProperty(PropertyName = "Network")]
	[JsonConverter(typeof(NetworkJsonConverter))]
	public Network Network { get; set; } = Network.Main;

	[DefaultValue(Constants.BackendUri)]
	[JsonProperty(PropertyName = "MainNetBackendUri", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string MainNetBackendUri { get; init; } = Constants.BackendUri;

	[DefaultValue(Constants.TestnetBackendUri)]
	[JsonProperty(PropertyName = "TestNetClearnetBackendUri", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string TestNetBackendUri { get; init; } = Constants.TestnetBackendUri;

	[DefaultValue("http://localhost:37127/")]
	[JsonProperty(PropertyName = "RegTestBackendUri", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string RegTestBackendUri { get; init; } = "http://localhost:37127/";

	[JsonProperty(PropertyName = "MainNetCoordinatorUri", DefaultValueHandling = DefaultValueHandling.Ignore)]
	public string? MainNetCoordinatorUri { get; init; }

	[JsonProperty(PropertyName = "TestNetCoordinatorUri", DefaultValueHandling = DefaultValueHandling.Ignore)]
	public string? TestNetCoordinatorUri { get; init; }

	[JsonProperty(PropertyName = "RegTestCoordinatorUri", DefaultValueHandling = DefaultValueHandling.Ignore)]
	public string? RegTestCoordinatorUri { get; init; }

	[DefaultValue(true)]
	[JsonProperty(PropertyName = "UseTor", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool UseTor { get; init; } = true;

	[DefaultValue(false)]
	[JsonProperty(PropertyName = "TerminateTorOnExit", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool TerminateTorOnExit { get; init; } = false;

	[DefaultValue(true)]
	[JsonProperty(PropertyName = "DownloadNewVersion", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool DownloadNewVersion { get; init; } = true;

	[DefaultValue(false)]
	[JsonProperty(PropertyName = "StartLocalBitcoinCoreOnStartup", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool StartLocalBitcoinCoreOnStartup { get; init; } = false;

	[DefaultValue(true)]
	[JsonProperty(PropertyName = "StopLocalBitcoinCoreOnShutdown", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool StopLocalBitcoinCoreOnShutdown { get; init; } = true;

	[JsonProperty(PropertyName = "LocalBitcoinCoreDataDir")]
	public string LocalBitcoinCoreDataDir { get; init; } = EnvironmentHelpers.GetDefaultBitcoinCoreDataDirOrEmptyString();

	[JsonProperty(PropertyName = "MainNetBitcoinP2pEndPoint")]
	[JsonConverter(typeof(EndPointJsonConverter), Constants.DefaultMainNetBitcoinP2pPort)]
	public EndPoint MainNetBitcoinP2pEndPoint { get; init; } = new IPEndPoint(IPAddress.Loopback, Constants.DefaultMainNetBitcoinP2pPort);

	[JsonProperty(PropertyName = "TestNetBitcoinP2pEndPoint")]
	[JsonConverter(typeof(EndPointJsonConverter), Constants.DefaultTestNetBitcoinP2pPort)]
	public EndPoint TestNetBitcoinP2pEndPoint { get; init; } = new IPEndPoint(IPAddress.Loopback, Constants.DefaultTestNetBitcoinP2pPort);

	[JsonProperty(PropertyName = "RegTestBitcoinP2pEndPoint")]
	[JsonConverter(typeof(EndPointJsonConverter), Constants.DefaultRegTestBitcoinP2pPort)]
	public EndPoint RegTestBitcoinP2pEndPoint { get; init; } = new IPEndPoint(IPAddress.Loopback, Constants.DefaultRegTestBitcoinP2pPort);

	[DefaultValue(false)]
	[JsonProperty(PropertyName = "JsonRpcServerEnabled", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool JsonRpcServerEnabled { get; init; }

	[DefaultValue("")]
	[JsonProperty(PropertyName = "JsonRpcUser", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string JsonRpcUser { get; init; } = "";

	[DefaultValue("")]
	[JsonProperty(PropertyName = "JsonRpcPassword", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string JsonRpcPassword { get; init; } = "";

	[JsonProperty(PropertyName = "JsonRpcServerPrefixes")]
	public string[] JsonRpcServerPrefixes { get; init; } = new[]
	{
		"http://127.0.0.1:37128/",
		"http://localhost:37128/"
	};

	[JsonProperty(PropertyName = "DustThreshold")]
	[JsonConverter(typeof(MoneyBtcJsonConverter))]
	public Money DustThreshold { get; init; } = DefaultDustThreshold;

	[JsonProperty(PropertyName = "EnableGpu")]
	public bool EnableGpu { get; init; } = true;

	[DefaultValue("CoinJoinCoordinatorIdentifier")]
	[JsonProperty(PropertyName = "CoordinatorIdentifier", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string CoordinatorIdentifier { get; init; } = "CoinJoinCoordinatorIdentifier";

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

	public bool MigrateOldDefaultBackendUris([NotNullWhenAttribute(true)] out PersistentConfig? newConfig)
	{
		bool hasChanged = false;
		newConfig = null;

		if (MainNetBackendUri == "https://wasabiwallet.io/" || TestNetBackendUri == "https://wasabiwallet.co/")
		{
			hasChanged = true;
			newConfig = this with
			{
				MainNetBackendUri = "https://api.wasabiwallet.io/",
				TestNetBackendUri = "https://api.wasabiwallet.co/",
			};
		}

		return hasChanged;
	}
}
