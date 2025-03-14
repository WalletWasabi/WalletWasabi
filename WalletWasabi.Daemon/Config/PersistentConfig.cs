using System.IO;
using NBitcoin;
using System.Linq;
using System.Net;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Userfacing;

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

	public string MainNetBitcoinRpcCredentialString { get; init; } = "";
	public string TestNetBitcoinRpcCredentialString { get; init; } = "";
	public string RegTestBitcoinRpcCredentialString { get; init; } = "";

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
			MainNetBitcoinRpcCredentialString.Equals(other.MainNetBitcoinRpcCredentialString) &&
			TestNetBitcoinRpcCredentialString.Equals(other.TestNetBitcoinRpcCredentialString) &&
			RegTestBitcoinRpcCredentialString.Equals(other.RegTestBitcoinRpcCredentialString) &&
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

	public string GetBitcoinRpcCredentialString()
	{
		if (Network == Network.Main)
		{
			return MainNetBitcoinRpcCredentialString;
		}

		if (Network == Network.TestNet)
		{
			return TestNetBitcoinRpcCredentialString;
		}

		if (Network == Network.RegTest)
		{
			return RegTestBitcoinRpcCredentialString;
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

	public PersistentConfig Migrate() =>
		MigrateMaxCoordinationFeeRate()
		.MigrateOldDefaultBackendUris()
		.MigrateP2pToRpcConnection() with
		{
			ConfigVersion = 2
		};

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

	private PersistentConfig MigrateP2pToRpcConnection()
	{
		if (ConfigVersion >= 2)
		{
			return this;
		}

		static string? GetRpcCredentialString(BitcoinConfig config, string network, string bitcoindatadir) =>
			( config.GetSettingOrNull("rpcuser", network)
			, config.GetSettingOrNull("rpcpassword", network)
			, config.GetSettingOrNull("rpccookiefile", network)
			) switch
			{
				({ } rpcUser, { } rpcPassword, _) => $"{rpcUser}:{rpcPassword}",
				( _, null, { } cookieFilePath) => $"cookiefile={cookieFilePath}",
				( _, null, _) => $"cookiefile={bitcoindatadir}/.cookie",
				_ => null
			};
		static EndPoint GetRpcEndpoint(BitcoinConfig config, string network, int defaultPort) =>
			(config.GetSettingOrNull("rpcbind", network), config.GetSettingOrNull("rpcport", network)) switch
			{
				({} host, {} port) when EndPointParser.TryParse($"{host}:{port}", defaultPort, out var endPoint) => endPoint,
				({} host, null) when EndPointParser.TryParse(host, defaultPort, out var endPoint) => endPoint,
				(null, {} port) when int.TryParse(port, out var intPort) => new IPEndPoint(IPAddress.Loopback, intPort),
				_ => new IPEndPoint(IPAddress.Loopback, defaultPort)
			};

		var defaultBitcoinDataDir = Network.GetDefaultDataFolder("bitcoin");
		if (string.IsNullOrWhiteSpace(defaultBitcoinDataDir))
		{
			return this;
		}

		try
		{
			var configPath = Path.Combine(defaultBitcoinDataDir, "bitcoin.conf");
			var bitcoinConfig = File.ReadAllText(configPath);
			var config = BitcoinConfig.Parse(bitcoinConfig);

			return this with
			{
				MainNetBitcoinRpcEndPoint = GetRpcEndpoint(config, "main", Network.Main.RPCPort),
				TestNetBitcoinRpcEndPoint = GetRpcEndpoint(config, "testnet4", Network.TestNet.RPCPort),
				RegTestBitcoinRpcEndPoint = GetRpcEndpoint(config, "regtest", Network.RegTest.RPCPort),

				MainNetBitcoinRpcCredentialString = GetRpcCredentialString(config, "main", defaultBitcoinDataDir) ?? "",
				TestNetBitcoinRpcCredentialString = GetRpcCredentialString(config, "testnet4", defaultBitcoinDataDir) ?? "",
				RegTestBitcoinRpcCredentialString = GetRpcCredentialString(config, "regtest", defaultBitcoinDataDir) ?? "",
			};
		}
		catch (IOException e)
		{
			Logger.LogError("It was not possible to read the bitcoin's config file.", e);
			return this;
		}
	}
}
