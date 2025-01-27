using NBitcoin;
using System.Linq;
using System.Net;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;

namespace WalletWasabi.Daemon;

public record PersistentConfig
{
	public Network Network { get; init; } = Network.Main;

	public string MainNetBackendUri { get; init; } = Constants.BackendUri;

	public string TestNetBackendUri { get; init; } = Constants.TestnetBackendUri;

	public string RegTestBackendUri { get; init; } = "http://localhost:37127/";

	public string MainNetCoordinatorUri { get; init; } = "";

	public string TestNetCoordinatorUri { get; init; } = "";

	public string RegTestCoordinatorUri { get; init; } = "http://localhost:37128/";

	/// <remarks>
	/// For backward compatibility this was changed to an object.
	/// Only strings (new) and booleans (old) are supported.
	/// </remarks>
	public object UseTor { get; init; } = "Enabled";

	public bool TerminateTorOnExit { get; init; }

	public string[] TorBridges { get; init; } = [];

	public bool DownloadNewVersion { get; init; } = true;

	public bool StartLocalBitcoinCoreOnStartup { get; init; }

	public bool StopLocalBitcoinCoreOnShutdown { get; init; } = true;

	public string LocalBitcoinCoreDataDir { get; init; } = EnvironmentHelpers.GetDefaultBitcoinCoreDataDirOrEmptyString();

	public EndPoint MainNetBitcoinP2pEndPoint { get; init; } = Constants.DefaultMainNetBitcoinP2PEndPoint;

	public EndPoint TestNetBitcoinP2pEndPoint { get; init; } = Constants.DefaultTestNetBitcoinP2PEndPoint;

	public EndPoint RegTestBitcoinP2pEndPoint { get; init; } = Constants.DefaultRegTestBitcoinP2PEndPoint;

	public bool JsonRpcServerEnabled { get; init; }

	public string JsonRpcUser { get; init; } = "";

	public string JsonRpcPassword { get; init; } = "";

	public string[] JsonRpcServerPrefixes { get; init; } =
	[
		"http://127.0.0.1:37128/",
		"http://localhost:37128/"
	];

	public Money DustThreshold { get; init; } = Money.Coins(Constants.DefaultDustThreshold);

	public bool EnableGpu { get; init; } = true;

	public string CoordinatorIdentifier { get; init; } = "CoinJoinCoordinatorIdentifier";

	public string ExchangeRateProvider { get; init; } = "MempoolSpace";

	public string  FeeRateEstimationProvider { get; init; } = "BlockstreamInfo";

	public decimal MaxCoinJoinMiningFeeRate { get; init; } = Constants.DefaultMaxCoinJoinMiningFeeRate;

	public int AbsoluteMinInputCount { get; init; } = Constants.DefaultAbsoluteMinInputCount;
	public int ConfigVersion { get; init; }

	public bool DeepEquals(PersistentConfig other)
	{
		bool useTorIsEqual = Config.ObjectToTorMode(UseTor) == Config.ObjectToTorMode(other.UseTor);

		return
			ConfigVersion == other.ConfigVersion &&
			Network == other.Network &&
			MainNetBackendUri == other.MainNetBackendUri &&
			TestNetBackendUri == other.TestNetBackendUri &&
			RegTestBackendUri == other.RegTestBackendUri &&
			MainNetCoordinatorUri == other.MainNetCoordinatorUri &&
			TestNetCoordinatorUri == other.TestNetCoordinatorUri &&
			RegTestCoordinatorUri == other.RegTestCoordinatorUri &&
			useTorIsEqual &&
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
			MaxCoinJoinMiningFeeRate == other.MaxCoinJoinMiningFeeRate &&
			AbsoluteMinInputCount == other.AbsoluteMinInputCount &&
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

	public string GetBackendUri()
	{
		if (Network == Network.Main)
		{
			return MainNetBackendUri;
		}

		if (Network == Network.TestNet)
		{
			return TestNetBackendUri;
		}

		if (Network == Network.RegTest)
		{
			return RegTestBackendUri;
		}

		throw new NotSupportedNetworkException(Network);
	}

	public PersistentConfig Migrate()
	{
		if (ConfigVersion == 0)
		{
			return MigrateMaxCoordinationFeeRate().MigrateOldDefaultBackendUris() with
			{
				ConfigVersion = 1
			};
		}

		return this;
	}

	private PersistentConfig MigrateMaxCoordinationFeeRate() => this;

	private PersistentConfig MigrateOldDefaultBackendUris()
	{
		if (MainNetBackendUri == "https://wasabiwallet.io/" || TestNetBackendUri == "https://wasabiwallet.co/")
		{
			return this with
			{
				MainNetBackendUri = "https://api.wasabiwallet.io/",
				TestNetBackendUri = "https://api.wasabiwallet.co/",
			};
		}

		return this;
	}
}
