using System.Linq;
using System.Net;
using System.Text.Json.Nodes;
using NBitcoin;
using WalletWasabi.Discoverability;
using WalletWasabi.Helpers;
using WalletWasabi.Userfacing;
using WalletWasabi.WabiSabi.Coordinator;

namespace WalletWasabi.Serialization;

public static partial class Encode
{
	public static JsonNode EndPoint(EndPoint ep, int defaultPort) => String(ep.ToString(defaultPort));

	public static JsonNode ExtPubKey(ExtPubKey k) => String(k.GetWif(NBitcoin.Network.Main).ToWif());

	public static JsonNode AnnouncerConfig(AnnouncerConfig cfg) =>
		Object([
			("CoordinatorName", String(cfg.CoordinatorName)),
			("IsEnabled", Bool(cfg.IsEnabled)),
			("CoordinatorDescription", String(cfg.CoordinatorDescription)),
			("CoordinatorUri", String(cfg.CoordinatorUri)),
			("AbsoluteMinInputCount", UInt(cfg.AbsoluteMinInputCount)),
			("ReadMoreUri", String(cfg.ReadMoreUri)),
			("RelayUris", Array(cfg.RelayUris.Select(String))),
			("Key", String(cfg.Key))
		]);

	public static JsonNode WabiSabiConfig(WabiSabiConfig cfg) =>
		Object([
			("Network", Network(cfg.Network)),
			("BitcoinCoreRpcEndPoint", String(cfg.BitcoinRpcUri)),
			("BitcoinRpcConnectionString", String(cfg.BitcoinRpcConnectionString)),
			("ConfirmationTarget", UInt(cfg.ConfirmationTarget)),
			("DoSSeverity", MoneyBitcoins(cfg.DoSSeverity)),
			("DoSMinTimeForFailedToVerify", TimeSpan(cfg.DoSMinTimeForFailedToVerify) ),
			("DoSMinTimeForCheating", TimeSpan(cfg.DoSMinTimeForCheating) ),
			("DoSPenaltyFactorForDisruptingConfirmation", Double(cfg.DoSPenaltyFactorForDisruptingConfirmation) ),
			("DoSPenaltyFactorForDisruptingSignalReadyToSign", Double(cfg.DoSPenaltyFactorForDisruptingSignalReadyToSign) ),
			("DoSPenaltyFactorForDisruptingSigning", Double(cfg.DoSPenaltyFactorForDisruptingSigning) ),
			("DoSPenaltyFactorForDisruptingByDoubleSpending", Double(cfg.DoSPenaltyFactorForDisruptingByDoubleSpending) ),
			("DoSMinTimeInPrison", TimeSpan(cfg.DoSMinTimeInPrison) ),
			("MinRegistrableAmount", MoneyBitcoins(cfg.MinRegistrableAmount) ),
			("MaxRegistrableAmount", MoneyBitcoins(cfg.MaxRegistrableAmount) ),
			("AllowNotedInputRegistration", Bool(cfg.AllowNotedInputRegistration) ),
			("StandardInputRegistrationTimeout", TimeSpan(cfg.StandardInputRegistrationTimeout) ),
			("BlameInputRegistrationTimeout", TimeSpan(cfg.BlameInputRegistrationTimeout) ),
			("ConnectionConfirmationTimeout",  TimeSpan(cfg.ConnectionConfirmationTimeout)),
			("OutputRegistrationTimeout", TimeSpan(cfg.OutputRegistrationTimeout)),
			("TransactionSigningTimeout", TimeSpan(cfg.TransactionSigningTimeout) ),
			("FailFastOutputRegistrationTimeout",  TimeSpan(cfg.FailFastOutputRegistrationTimeout)),
			("FailFastTransactionSigningTimeout", TimeSpan(cfg.FailFastTransactionSigningTimeout) ),
			("RoundExpiryTimeout", TimeSpan(cfg.RoundExpiryTimeout) ),
			("MaxInputCountByRound", Int(cfg.MaxInputCountByRound) ),
			("MinInputCountByRoundMultiplier", Double(cfg.MinInputCountByRoundMultiplier) ),
			("MinInputCountByBlameRoundMultiplier", Double(cfg.MinInputCountByBlameRoundMultiplier) ),
			("RoundDestroyerThreshold", Int(cfg.RoundDestroyerThreshold) ),
			("CoordinatorExtPubKey", ExtPubKey(cfg.CoordinatorExtPubKey) ),
			("CoordinatorExtPubKeyCurrentDepth", Int(cfg.CoordinatorExtPubKeyCurrentDepth) ),
			("MaxSuggestedAmountBase", MoneyBitcoins(cfg.MaxSuggestedAmountBase) ),
			("RoundParallelization", Int(cfg.RoundParallelization) ),
			("WW200CompatibleLoadBalancing", Bool(cfg.WW200CompatibleLoadBalancing) ),
			("WW200CompatibleLoadBalancingInputSplit", Double(cfg.WW200CompatibleLoadBalancingInputSplit) ),
			("CoordinatorIdentifier", String(cfg.CoordinatorIdentifier) ),
			("AllowP2wpkhInputs", Bool(cfg.AllowP2wpkhInputs) ),
			("AllowP2trInputs", Bool(cfg.AllowP2trInputs)),
			("AllowP2wpkhOutputs", Bool(cfg.AllowP2wpkhOutputs)),
			("AllowP2trOutputs", Bool(cfg.AllowP2trOutputs)),
			("AllowP2pkhOutputs", Bool(cfg.AllowP2pkhOutputs)),
			("AllowP2shOutputs", Bool(cfg.AllowP2shOutputs)),
			("AllowP2wshOutputs", Bool(cfg.AllowP2wshOutputs)),
			("DelayTransactionSigning", Bool(cfg.DelayTransactionSigning)),
			("AnnouncerConfig", AnnouncerConfig(cfg.AnnouncerConfig)),
		]);
}

public static partial class Decode
{
	public static readonly Decoder<EndPoint> EndPoint =
		String.AndThen(s =>
		{
			if (EndPointParser.TryParse(s, out EndPoint? endPoint))
			{
				return Succeed(endPoint);
			}

			return Fail<EndPoint>($"Invalid endpoint format: '{s}'");
		});

	public static readonly Decoder<ExtPubKey> ExtPubKey =
		String.Map(s => NBitcoin.ExtPubKey.Parse(s, NBitcoin.Network.Main)).Catch();

	public static readonly Decoder<AnnouncerConfig> AnnouncerConfig =
		Object(get => new AnnouncerConfig{
			CoordinatorName = get.Required("CoordinatorName", String),
			IsEnabled = get.Required("IsEnabled", Bool),
			CoordinatorDescription = get.Required("CoordinatorDescription", String),
			CoordinatorUri = get.Required("CoordinatorUri", String),
			AbsoluteMinInputCount = get.Required("AbsoluteMinInputCount", UInt),
			ReadMoreUri = get.Required("ReadMoreUri", String),
			RelayUris = get.Required("RelayUris", Array(String)),
			Key = get.Required("Key", String)
		});

	public static Decoder<WabiSabiConfig> WabiSabiConfig(string filePath) =>
		Object(get => new WabiSabiConfig(filePath)
		{
			Network = get.Required("Network", Network),
			BitcoinRpcUri = get.Required("BitcoinCoreRpcEndPoint", String),
			BitcoinRpcConnectionString = get.Required("BitcoinRpcConnectionString", String),
			ConfirmationTarget = get.Required("ConfirmationTarget", UInt),
			DoSSeverity = get.Required("DoSSeverity", MoneyBitcoins),
			DoSMinTimeForFailedToVerify = get.Required("DoSMinTimeForFailedToVerify", TimeSpan),
			DoSMinTimeForCheating = get.Required("DoSMinTimeForCheating", TimeSpan),
			DoSPenaltyFactorForDisruptingConfirmation = get.Required("DoSPenaltyFactorForDisruptingConfirmation", Double),
			DoSPenaltyFactorForDisruptingSignalReadyToSign = get.Required("DoSPenaltyFactorForDisruptingSignalReadyToSign", Double),
			DoSPenaltyFactorForDisruptingSigning = get.Required("DoSPenaltyFactorForDisruptingSigning", Double),
			DoSPenaltyFactorForDisruptingByDoubleSpending = get.Required("DoSPenaltyFactorForDisruptingByDoubleSpending", Double),
			DoSMinTimeInPrison = get.Required("DoSMinTimeInPrison", TimeSpan),
			MinRegistrableAmount = get.Required("MinRegistrableAmount", MoneyBitcoins),
			MaxRegistrableAmount = get.Required("MaxRegistrableAmount", MoneyBitcoins),
			AllowNotedInputRegistration = get.Required("AllowNotedInputRegistration", Bool),
			StandardInputRegistrationTimeout = get.Required("StandardInputRegistrationTimeout", TimeSpan),
			BlameInputRegistrationTimeout = get.Required("BlameInputRegistrationTimeout", TimeSpan),
			ConnectionConfirmationTimeout = get.Required("ConnectionConfirmationTimeout", TimeSpan),
			OutputRegistrationTimeout = get.Required("OutputRegistrationTimeout", TimeSpan),
			TransactionSigningTimeout = get.Required("TransactionSigningTimeout", TimeSpan),
			FailFastOutputRegistrationTimeout = get.Required("FailFastOutputRegistrationTimeout", TimeSpan),
			FailFastTransactionSigningTimeout = get.Required("FailFastTransactionSigningTimeout", TimeSpan),
			RoundExpiryTimeout = get.Required("RoundExpiryTimeout", TimeSpan),
			MaxInputCountByRound = get.Required("MaxInputCountByRound", Int),
			MinInputCountByRoundMultiplier = get.Required("MinInputCountByRoundMultiplier", Double),
			MinInputCountByBlameRoundMultiplier = get.Required("MinInputCountByBlameRoundMultiplier", Double),
			RoundDestroyerThreshold = get.Required("RoundDestroyerThreshold", Int),
			CoordinatorExtPubKey = get.Required("CoordinatorExtPubKey", ExtPubKey),
			CoordinatorExtPubKeyCurrentDepth = get.Required("CoordinatorExtPubKeyCurrentDepth", Int),
			MaxSuggestedAmountBase = get.Required("MaxSuggestedAmountBase", MoneyBitcoins),
			RoundParallelization = get.Required("RoundParallelization", Int),
			WW200CompatibleLoadBalancing = get.Required("WW200CompatibleLoadBalancing", Bool),
			WW200CompatibleLoadBalancingInputSplit = get.Required("WW200CompatibleLoadBalancingInputSplit", Double),
			CoordinatorIdentifier = get.Required("CoordinatorIdentifier", String),
			AllowP2wpkhInputs = get.Required("AllowP2wpkhInputs", Bool),
			AllowP2trInputs = get.Required("AllowP2trInputs", Bool),
			AllowP2wpkhOutputs = get.Required("AllowP2wpkhOutputs", Bool),
			AllowP2trOutputs = get.Required("AllowP2trOutputs", Bool),
			AllowP2pkhOutputs = get.Required("AllowP2pkhOutputs", Bool),
			AllowP2shOutputs = get.Required("AllowP2shOutputs", Bool),
			AllowP2wshOutputs = get.Required("AllowP2wshOutputs", Bool),
			DelayTransactionSigning = get.Required("DelayTransactionSigning", Bool),
			AnnouncerConfig = get.Required("AnnouncerConfig", AnnouncerConfig)
		});
}
