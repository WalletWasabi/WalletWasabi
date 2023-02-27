using NBitcoin;
using NBitcoin.Protocol;

namespace WalletWasabi.Helpers;

public static class Constants
{
	public const string ClientSupportBackendVersionMin = "4";
	public const string ClientSupportBackendVersionMax = "4";
	public const string BackendMajorVersion = "4";

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

	/// <summary>
	/// OBSOLATED, USE SPECIFIC TYPE
	/// </summary>
	public const int OutputSizeInBytes = 33;

	// https://en.bitcoin.it/wiki/Bitcoin
	// There are a maximum of 2,099,999,997,690,000 Bitcoin elements (called satoshis), which are currently most commonly measured in units of 100,000,000 known as BTC. Stated another way, no more than 21 million BTC can ever be created.
	public const long MaximumNumberOfSatoshis = 2099999997690000;

	public const decimal MaximumNumberOfBitcoins = 20999999.9769m;

	public const int SemiPrivateThreshold = 2;

	public const int FastestConfirmationTarget = 1;
	public const int TwentyMinutesConfirmationTarget = 2;
	public const int OneDayConfirmationTarget = 144;
	public const int SevenDaysConfirmationTarget = 1008;

	public const int BigFileReadWriteBufferSize = 1 * 1024 * 1024;

	public const int DefaultMainNetBitcoinP2pPort = 8333;
	public const int DefaultTestNetBitcoinP2pPort = 18333;
	public const int DefaultRegTestBitcoinP2pPort = 18444;

	public const int DefaultMainNetBitcoinCoreRpcPort = 8332;
	public const int DefaultTestNetBitcoinCoreRpcPort = 18332;
	public const int DefaultRegTestBitcoinCoreRpcPort = 18443;

	public const decimal DefaultDustThreshold = 0.00005m;

	public const string AlphaNumericCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
	public const string CapitalAlphaNumericCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

	public const string ExecutableName = "wassabee";
	public const string AppName = "Wasabi Wallet";
	public const string BuiltinBitcoinNodeName = "Bitcoin Knots";

	public const string FallbackAffiliationMessageSignerKey = "30770201010420686710a86f0cdf425e3bc9781f51e45b9440aec1215002402d5cdee713066623a00a06082a8648ce3d030107a14403420004f267804052bd863a1644233b8bfb5b8652ab99bcbfa0fb9c36113a571eb5c0cb7c733dbcf1777c2745c782f96e218bb71d67d15da1a77d37fa3cb96f423e53ba";

	public static readonly Money MaximumNumberOfBitcoinsMoney = Money.Coins(MaximumNumberOfBitcoins);

	public static readonly Version ClientVersion = new(2, 0, 2, 1);

	public static readonly Version HwiVersion = new("2.1.1");
	public static readonly Version BitcoinCoreVersion = new("21.2");
	public static readonly Version Ww1LegalDocumentsVersion = new(3, 0);
	public static readonly Version Ww2LegalDocumentsVersion = new(1, 0);

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

	public static readonly ExtPubKey FallBackCoordinatorExtPubKey = NBitcoinHelpers.BetterParseExtPubKey("xpub6BgAZqHhxw6pgEi2F38w5RBqctqCEoVWqcMdrn1epQZceKHtn8f8zHBduM3fwYQEKEGUf4efD6qRPc9wvDF4neoc6JjDbHNiaHbs3we5qL3");
	public static readonly ExtPubKey WabiSabiFallBackCoordinatorExtPubKey = NBitcoinHelpers.BetterParseExtPubKey("xpub6C13JhXzjAhVRgeTcRSWqKEPe1vHi3Tmh2K9PN1cZaZFVjjSaj76y5NNyqYjc2bugj64LVDFYu8NZWtJsXNYKFb9J94nehLAPAKqKiXcebC");
	public static readonly PubKey WasabiPubKey = new("02c8ab8eea76c83788e246a1baee10c04a134ec11be6553946f6ae65e47ae9a608");

	public static readonly string[] UserAgents = new[]
	{
		"/Satoshi:24.0.1/",
		"/Satoshi:23.1.0/",
		"/Satoshi:23.0.0/",
		"/Satoshi:22.1.0/",
		"/Satoshi:22.0.0/",
		"/Satoshi:0.21.1/",
		"/Satoshi:0.21.0/",
		"/Satoshi:0.20.1/",
		"/Satoshi:0.20.0/",
		"/Satoshi:0.19.1/",
		"/Satoshi:0.19.0.1/",
		"/Satoshi:0.19.0/",
		"/Satoshi:0.18.1/",
		"/Satoshi:0.18.0/",
		"/Satoshi:0.17.1/",
		"/Satoshi:0.17.0.1/",
		"/Satoshi:0.17.0/",
		"/Satoshi:0.16.3/",
		"/Satoshi:0.16.2/",
		"/Satoshi:0.16.1/",
		"/Satoshi:0.16.0/",
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

	public static string ClientSupportBackendVersionText => ClientSupportBackendVersionMin == ClientSupportBackendVersionMax
		? ClientSupportBackendVersionMin
		: $"{ClientSupportBackendVersionMin} - {ClientSupportBackendVersionMax}";
}
