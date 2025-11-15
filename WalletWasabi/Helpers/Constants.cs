using System.Net;
using NBitcoin;
using NBitcoin.Protocol;

namespace WalletWasabi.Helpers;

public static class Constants
{
	public const string BackendMajorVersion = "4";
	public const string ClientSupportBackendVersion = "4";

	public const string IndexerUri = "https://api.wasabiwallet.io/";
	public const string TestnetIndexerUri = "https://api.wasabiwallet.co/";
	public const string RegTestIndexerUri = "http://localhost:37127/";

	public const string CoordinatorUri = "";
	public const string TestnetCoordinatorUri = "";
	public const string RegTestCoordinatorUri = "http://localhost:37126/";

	public const string WabiSabiFallBackCoordinatorExtPubKey = "xpub6C13JhXzjAhVRgeTcRSWqKEPe1vHi3Tmh2K9PN1cZaZFVjjSaj76y5NNyqYjc2bugj64LVDFYu8NZWtJsXNYKFb9J94nehLAPAKqKiXcebC";
	public const string WasabiPubKey = "02c8ab8eea76c83788e246a1baee10c04a134ec11be6553946f6ae65e47ae9a608";

	public const string DonationAddress = "sp1qq2exrz9xjumnvujw7zmav4r3vhfj9rvmd0aytjx0xesvzlmn48ctgqnqdgaan0ahmcfw3cpq5nxvnczzfhhvl3hmsps683cap4y696qecs7wejl3";

	public const string WasabiTeamNostrPubKey = "npub129hpcwy3h7uhpzwzts6utkt2p5st7lf4qpzp3d2j0p6z56lvkpgspngzeq";

	/// <summary>
	/// By changing this, we can force to start over the transactions file, so old incorrect transactions would be cleared.
	/// It is also important to force the KeyManagers to be reindexed when this is changed by renaming the BlockState Height related property.
	/// </summary>
	public const string ConfirmedTransactionsVersion = "2";

	public const uint ProtocolVersionWitnessVersion = 70012;

	public const int InputBaseSizeInBytes = 41;

	public const int P2wpkhInputSizeInBytes = 41;
	public const int P2wpkhInputVirtualSize = 69;
	public const int P2pkhInputSizeInBytes = 145;
	public const int P2wpkhOutputVirtualSize = 31;

	public const int P2trInputVirtualSize = 58;
	public const int P2trOutputVirtualSize = 43;

	public const int P2pkhInputVirtualSize = 148;
	public const int P2pkhOutputVirtualSize = 34;
	public const int P2wshInputVirtualSize = 105; // we assume a 2-of-n multisig
	public const int P2wshOutputVirtualSize = 32;
	public const int P2shInputVirtualSize = 297; // we assume a 2-of-n multisig
	public const int P2shOutputVirtualSize = 32;

	// https://en.bitcoin.it/wiki/Bitcoin
	// There are a maximum of 2,099,999,997,690,000 Bitcoin elements (called satoshis), which are currently most commonly measured in units of 100,000,000 known as BTC. Stated another way, no more than 21 million BTC can ever be created.
	public const long MaximumNumberOfSatoshis = 2099999997690000;

	public const decimal MaximumNumberOfBitcoins = 20999999.9769m;

	public const int SemiPrivateThreshold = 2;

	public const int FastestConfirmationTarget = 1;
	public const int TwentyMinutesConfirmationTarget = 2;
	public const int OneDayConfirmationTarget = 144;
	public const int SevenDaysConfirmationTarget = 1008;

	public const int DefaultMainNetBitcoinRpcPort = 8332;
	public const int DefaultTestNetBitcoinRpcPort = 48332;
	public const int DefaultRegTestBitcoinCorePort = 18443;

	public const decimal DefaultDustThreshold = 0.00005m;
	public const decimal DefaultMaxCoinJoinMiningFeeRate = 150.0m;
	public const int DefaultAbsoluteMinInputCount = 21;
	public const int AbsoluteMinInputCount = 2;

	public const string AlphaNumericCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
	public const string CapitalAlphaNumericCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

	/// <summary>Executable file name of Wasabi Wallet Daemon application (without extension).</summary>
	public const string DaemonExecutableName = $"{ExecutableName}d";

	/// <summary>Executable file name of Wasabi Wallet UI application (without extension).</summary>
	public const string ExecutableName = "wassabee";

	/// <summary>Plist name, only for MacOs. Starts Wasabi with -startsilent argument.</summary>
	public const string SilentPlistName = "com.wasabiwallet.startup.plist";

	public const string AppName = "Wasabi Wallet";

	public static readonly string DefaultMainNetBitcoinRpcUri = $"http://localhost:{DefaultMainNetBitcoinRpcPort}";
	public static readonly string DefaultTestNetBitcoinRpcUri = $"http://localhost:{DefaultTestNetBitcoinRpcPort}";
	public static readonly string DefaultRegTestBitcoinRpcUri = $"http://localhost:{DefaultRegTestBitcoinCorePort}";

	public static readonly string DefaultExchangeRateProvider = "MempoolSpace";
	public static readonly string DefaultFeeRateEstimationProvider = "MempoolSpace";
	public static readonly string DefaultExternalTransactionBroadcaster= "MempoolSpace";

	public static readonly Money MaximumNumberOfBitcoinsMoney = Money.Coins(MaximumNumberOfBitcoins);

	public static readonly Version ClientVersion = new(2, 7, 2);
	public static readonly string VersionName = "";

	public static readonly Version HwiVersion = new("3.1.0");

	public static readonly FeeRate MinRelayFeeRate = new(1m);
	public static readonly FeeRate AbsurdlyHighFeeRate = new(10_000m);

	public const decimal BnBMaximumDifferenceTolerance = 0.15m;
	public const int DefaultMaxDaysInMempool = 30;

	// Defined in hours. Do not modify these values or the order!
	public static readonly int[] CoinJoinFeeRateMedianTimeFrames = new[] { 24, 168, 720 };

	public static readonly NodeRequirement NodeRequirements = new()
	{
		RequiredServices = NodeServices.NODE_WITNESS,
		MinVersion = ProtocolVersionWitnessVersion,
		MinProtocolCapabilities = new ProtocolCapabilities { SupportGetBlock = true, SupportWitness = true, SupportMempoolQuery = true }
	};

	public static readonly NodeRequirement LocalNodeRequirements = new()
	{
		RequiredServices = NodeServices.NODE_WITNESS,
		MinVersion = ProtocolVersionWitnessVersion,
		MinProtocolCapabilities = new ProtocolCapabilities { SupportGetBlock = true, SupportWitness = true }
	};

	public static readonly string[] UserAgents = new[]
	{
		"/Satoshi:28.1.0/",
		"/Satoshi:28.0.0/",
		"/Satoshi:27.2.0/",
		"/Satoshi:27.1.0/",
		"/Satoshi:27.0.0/",
		"/Satoshi:26.2.0/",
		"/Satoshi:26.1.0/",
		"/Satoshi:26.0.0/",
		"/Satoshi:25.1.0/",
		"/Satoshi:25.0.0/",
		"/Satoshi:24.1.0/",
		"/Satoshi:24.0.1/",
		"/Satoshi:23.0.0/",
		"/Satoshi:22.0.0/",
		"/Satoshi:0.21.1/",
		"/Satoshi:0.21.0/",
		"/Satoshi:0.20.1/",
		"/Satoshi:0.20.0/",
	};

	public static readonly int[] ConfirmationTargets = new[]
	{
		2, // Twenty Minutes
		3, // Thirty Minutes
		6, // One Hour
		18, // Three Hours
		36, // Six Hours
		72, // Twelve Hours
		144, // One Day
		432, // Three Days
		1008, // Seven Days
	};

	public static readonly string DefaultFilterType = "legacy";
}
