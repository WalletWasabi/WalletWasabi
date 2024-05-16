using NBitcoin;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Text.Json.Serialization;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;
using WalletWasabi.Interfaces;
using WalletWasabi.JsonConverters;
using WalletWasabi.JsonConverters.Bitcoin;

namespace WalletWasabi.Daemon;

public record PersistentConfig : IConfigNg
{
	public const int DefaultJsonRpcServerPort = 37128;
	public static readonly Money DefaultDustThreshold = Money.Coins(Constants.DefaultDustThreshold);

	[JsonPropertyName("Network")]
	[JsonConverter(typeof(NetworkJsonConverterNg))]
	public Network Network { get; set; } = Network.Main;

	[DefaultValue(Constants.BackendUri)]
	[JsonPropertyName("MainNetBackendUri")]
	public string MainNetBackendUri { get; init; } = Constants.BackendUri;

	[DefaultValue(Constants.TestnetBackendUri)]
	[JsonPropertyName("TestNetClearnetBackendUri")]
	public string TestNetBackendUri { get; init; } = Constants.TestnetBackendUri;

	[DefaultValue("http://localhost:37127/")]
	[JsonPropertyName("RegTestBackendUri")]
	public string RegTestBackendUri { get; init; } = "http://localhost:37127/";

	[JsonPropertyName("MainNetCoordinatorUri")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? MainNetCoordinatorUri { get; init; }

	[JsonPropertyName("TestNetCoordinatorUri")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? TestNetCoordinatorUri { get; init; }

	[JsonPropertyName("RegTestCoordinatorUri")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? RegTestCoordinatorUri { get; init; }

	[DefaultValue(true)]
	[JsonPropertyName("UseTor")]
	public bool UseTor { get; init; } = true;

	[DefaultValue(false)]
	[JsonPropertyName("TerminateTorOnExit")]
	public bool TerminateTorOnExit { get; init; } = false;

	[DefaultValue(true)]
	[JsonPropertyName("TorBridges")]
	public string[] TorBridges { get; init; } = [];

	[DefaultValue(true)]
	[JsonPropertyName("DownloadNewVersion")]
	public bool DownloadNewVersion { get; init; } = true;

	[DefaultValue(false)]
	[JsonPropertyName("StartLocalBitcoinCoreOnStartup")]
	public bool StartLocalBitcoinCoreOnStartup { get; init; } = false;

	[DefaultValue(true)]
	[JsonPropertyName("StopLocalBitcoinCoreOnShutdown")]
	public bool StopLocalBitcoinCoreOnShutdown { get; init; } = true;

	[JsonPropertyName("LocalBitcoinCoreDataDir")]
	public string LocalBitcoinCoreDataDir { get; init; } = EnvironmentHelpers.GetDefaultBitcoinCoreDataDirOrEmptyString();

	[JsonPropertyName("MainNetBitcoinP2pEndPoint")]
	[JsonConverter(typeof(MainNetBitcoinP2pEndPointConverterNg))]
	public EndPoint MainNetBitcoinP2pEndPoint { get; init; } = new IPEndPoint(IPAddress.Loopback, Constants.DefaultMainNetBitcoinP2pPort);

	[JsonPropertyName("TestNetBitcoinP2pEndPoint")]
	[JsonConverter(typeof(TestNetBitcoinP2pEndPointConverterNg))]
	public EndPoint TestNetBitcoinP2pEndPoint { get; init; } = new IPEndPoint(IPAddress.Loopback, Constants.DefaultTestNetBitcoinP2pPort);

	[JsonPropertyName("RegTestBitcoinP2pEndPoint")]
	[JsonConverter(typeof(RegTestBitcoinP2pEndPointConverterNg))]
	public EndPoint RegTestBitcoinP2pEndPoint { get; init; } = new IPEndPoint(IPAddress.Loopback, Constants.DefaultRegTestBitcoinP2pPort);

	[DefaultValue(false)]
	[JsonPropertyName("JsonRpcServerEnabled")]
	public bool JsonRpcServerEnabled { get; init; }

	[DefaultValue("")]
	[JsonPropertyName("JsonRpcUser")]
	public string JsonRpcUser { get; init; } = "";

	[DefaultValue("")]
	[JsonPropertyName("JsonRpcPassword")]
	public string JsonRpcPassword { get; init; } = "";

	[JsonPropertyName("JsonRpcServerPrefixes")]
	public string[] JsonRpcServerPrefixes { get; init; } = new[]
	{
		"http://127.0.0.1:37128/",
		"http://localhost:37128/"
	};

	[JsonPropertyName("DustThreshold")]
	[JsonConverter(typeof(MoneyBtcJsonConverterNg))]
	public Money DustThreshold { get; init; } = DefaultDustThreshold;

	[JsonPropertyName("EnableGpu")]
	public bool EnableGpu { get; init; } = true;

	[DefaultValue("CoinJoinCoordinatorIdentifier")]
	[JsonPropertyName("CoordinatorIdentifier")]
	public string CoordinatorIdentifier { get; init; } = "CoinJoinCoordinatorIdentifier";

	[DefaultValue("MempoolSpace")]
	[JsonPropertyName("ExchangeRateProvider")]
	public string ExchangeRateProvider { get; init; } = "MempoolSpace";

	[DefaultValue("BlockstreamInfo")]
	[JsonPropertyName("FeeRateEstimationProvider")]
	public string  FeeRateEstimationProvider { get; init; } = "BlockstreamInfo";

	public bool DeepEquals(PersistentConfig other)
	{
		return
			Network == other.Network &&
			MainNetBackendUri == other.MainNetBackendUri &&
			TestNetBackendUri == other.TestNetBackendUri &&
			RegTestBackendUri == other.RegTestBackendUri &&
			MainNetCoordinatorUri == other.MainNetCoordinatorUri &&
			TestNetCoordinatorUri == other.TestNetCoordinatorUri &&
			RegTestCoordinatorUri == other.RegTestCoordinatorUri &&
			UseTor == other.UseTor &&
			TerminateTorOnExit == other.TerminateTorOnExit &&
			DownloadNewVersion == other.DownloadNewVersion &&
			StartLocalBitcoinCoreOnStartup == other.StartLocalBitcoinCoreOnStartup &&
			StopLocalBitcoinCoreOnShutdown == other.StopLocalBitcoinCoreOnShutdown &&
			LocalBitcoinCoreDataDir == other.LocalBitcoinCoreDataDir &&
			MainNetBitcoinP2pEndPoint.Equals(other.MainNetBitcoinP2pEndPoint) &&
			TestNetBitcoinP2pEndPoint.Equals(other.TestNetBitcoinP2pEndPoint) &&
			RegTestBitcoinP2pEndPoint.Equals(other.RegTestBitcoinP2pEndPoint) &&
			JsonRpcServerEnabled == other.JsonRpcServerEnabled &&
			JsonRpcUser == other.JsonRpcUser &&
			JsonRpcPassword == other.JsonRpcPassword &&
			JsonRpcServerPrefixes.SequenceEqual(other.JsonRpcServerPrefixes) &&
			DustThreshold == other.DustThreshold &&
			EnableGpu == other.EnableGpu &&
			CoordinatorIdentifier == other.CoordinatorIdentifier &&
			ExchangeRateProvider == other.ExchangeRateProvider &&
			FeeRateEstimationProvider == other.FeeRateEstimationProvider;
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

	public bool MigrateOldDefaultBackendUris([NotNullWhen(true)] out PersistentConfig? newConfig)
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
