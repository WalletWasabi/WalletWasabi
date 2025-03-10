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

	public string UseTor { get; init; } = "Enabled";

	public bool TerminateTorOnExit { get; init; }

	public string[] TorBridges { get; init; } = [];

	public bool DownloadNewVersion { get; init; } = true;

	public bool UseBitcoinRpc { get; init; }

	public string BitcoinRpcCredentialString { get; init; } = "";

	public EndPoint MainNetBitcoinRpcEndPoint { get; init; } = Constants.DefaultMainNetBitcoinCoreRpcEndPoint;

	public EndPoint TestNetBitcoinRpcEndPoint { get; init; } = Constants.DefaultTestNetBitcoinCoreRpcEndPoint;

	public EndPoint RegTestBitcoinRpcEndPoint { get; init; } = Constants.DefaultRegTestBitcoinCoreRpcEndPoint;

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

	public string  FeeRateEstimationProvider { get; init; } = "MempoolSpace";

	public decimal MaxCoinJoinMiningFeeRate { get; init; } = Constants.DefaultMaxCoinJoinMiningFeeRate;

	public int AbsoluteMinInputCount { get; init; } = Constants.DefaultAbsoluteMinInputCount;

	public int MaxDaysInMempool { get; init; } = Constants.DefaultMaxDaysInMempool;

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
			UseBitcoinRpc.Equals(other.UseBitcoinRpc) &&
			BitcoinRpcCredentialString.Equals(other.BitcoinRpcCredentialString) &&
			MainNetBitcoinRpcEndPoint.Equals(other.MainNetBitcoinRpcEndPoint) &&
			TestNetBitcoinRpcEndPoint.Equals(other.TestNetBitcoinRpcEndPoint) &&
			RegTestBitcoinRpcEndPoint.Equals(other.RegTestBitcoinRpcEndPoint) &&
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
			FeeRateEstimationProvider == other.FeeRateEstimationProvider &&
			MaxDaysInMempool == other.MaxDaysInMempool;
	}

	public EndPoint GetBitcoinRpcEndPoint()
	{
		if (Network == Network.Main)
		{
			return MainNetBitcoinRpcEndPoint;
		}
		if (Network == Network.TestNet)
		{
			return TestNetBitcoinRpcEndPoint;
		}
		if (Network == Network.RegTest)
		{
			return RegTestBitcoinRpcEndPoint;
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
