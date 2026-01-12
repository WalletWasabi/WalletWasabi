using System.Linq;
using System.Text.Json.Nodes;
using WalletWasabi.Discoverability;
using WalletWasabi.Serialization;
using static WalletWasabi.Serialization.Encode;
using static WalletWasabi.Serialization.Decode;

namespace WalletWasabi.Coordinator;

public static class ConfigEncode
{
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
			("MainNetBitcoinRpcUri", String(cfg.MainNetBitcoinRpcUri)),
			("TestNetBitcoinRpcUri", String(cfg.TestNetBitcoinRpcUri)),
			("RegTestBitcoinRpcUri", String(cfg.RegTestBitcoinRpcUri)),
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
			("PublishAsOnionService", Bool(cfg.PublishAsOnionService)),
			("OnionServicePrivateKey", Optional(cfg.OnionServicePrivateKey, String))
		]);
}

public static class ConfigDecode
{
	public static readonly Decoder<AnnouncerConfig> AnnouncerConfig =
		Object(get => new AnnouncerConfig{
			CoordinatorName = get.Required("CoordinatorName", Decode.String),
			IsEnabled = get.Required("IsEnabled", Decode.Bool),
			CoordinatorDescription = get.Required("CoordinatorDescription", Decode.String),
			CoordinatorUri = get.Required("CoordinatorUri", Decode.String),
			AbsoluteMinInputCount = get.Required("AbsoluteMinInputCount", Decode.UInt),
			ReadMoreUri = get.Required("ReadMoreUri", Decode.String),
			RelayUris = get.Required("RelayUris", Array(Decode.String)),
			Key = get.Required("Key", Decode.String)
		});

	public static Decoder<WabiSabiConfig> WabiSabiConfig(string filePath) =>
		Object(get => new WabiSabiConfig(filePath)
		{
			Network = get.Required("Network", Decode.Network),
			MainNetBitcoinRpcUri = get.Required("MainNetBitcoinRpcUri", Decode.String),
			TestNetBitcoinRpcUri = get.Required("TestNetBitcoinRpcUri", Decode.String),
			RegTestBitcoinRpcUri = get.Required("RegTestBitcoinRpcUri", Decode.String),
			BitcoinRpcConnectionString = get.Required("BitcoinRpcConnectionString", Decode.String),
			ConfirmationTarget = get.Required("ConfirmationTarget", Decode.UInt),
			DoSSeverity = get.Required("DoSSeverity", Decode.MoneyBitcoins),
			DoSMinTimeForFailedToVerify = get.Required("DoSMinTimeForFailedToVerify", Decode.TimeSpan),
			DoSMinTimeForCheating = get.Required("DoSMinTimeForCheating", Decode.TimeSpan),
			DoSPenaltyFactorForDisruptingConfirmation = get.Required("DoSPenaltyFactorForDisruptingConfirmation", Decode.Double),
			DoSPenaltyFactorForDisruptingSignalReadyToSign = get.Required("DoSPenaltyFactorForDisruptingSignalReadyToSign", Decode.Double),
			DoSPenaltyFactorForDisruptingSigning = get.Required("DoSPenaltyFactorForDisruptingSigning", Decode.Double),
			DoSPenaltyFactorForDisruptingByDoubleSpending = get.Required("DoSPenaltyFactorForDisruptingByDoubleSpending", Decode.Double),
			DoSMinTimeInPrison = get.Required("DoSMinTimeInPrison", Decode.TimeSpan),
			MinRegistrableAmount = get.Required("MinRegistrableAmount", Decode.MoneyBitcoins),
			MaxRegistrableAmount = get.Required("MaxRegistrableAmount", Decode.MoneyBitcoins),
			AllowNotedInputRegistration = get.Required("AllowNotedInputRegistration", Decode.Bool),
			StandardInputRegistrationTimeout = get.Required("StandardInputRegistrationTimeout", Decode.TimeSpan),
			BlameInputRegistrationTimeout = get.Required("BlameInputRegistrationTimeout", Decode.TimeSpan),
			ConnectionConfirmationTimeout = get.Required("ConnectionConfirmationTimeout", Decode.TimeSpan),
			OutputRegistrationTimeout = get.Required("OutputRegistrationTimeout", Decode.TimeSpan),
			TransactionSigningTimeout = get.Required("TransactionSigningTimeout", Decode.TimeSpan),
			FailFastOutputRegistrationTimeout = get.Required("FailFastOutputRegistrationTimeout", Decode.TimeSpan),
			FailFastTransactionSigningTimeout = get.Required("FailFastTransactionSigningTimeout", Decode.TimeSpan),
			RoundExpiryTimeout = get.Required("RoundExpiryTimeout", Decode.TimeSpan),
			MaxInputCountByRound = get.Required("MaxInputCountByRound", Decode.Int),
			MinInputCountByRoundMultiplier = get.Required("MinInputCountByRoundMultiplier", Decode.Double),
			MinInputCountByBlameRoundMultiplier = get.Required("MinInputCountByBlameRoundMultiplier", Decode.Double),
			RoundDestroyerThreshold = get.Required("RoundDestroyerThreshold", Decode.Int),
			CoordinatorExtPubKey = get.Required("CoordinatorExtPubKey", Decode.ExtPubKey),
			CoordinatorExtPubKeyCurrentDepth = get.Required("CoordinatorExtPubKeyCurrentDepth", Decode.Int),
			MaxSuggestedAmountBase = get.Required("MaxSuggestedAmountBase", Decode.MoneyBitcoins),
			RoundParallelization = get.Required("RoundParallelization", Decode.Int),
			CoordinatorIdentifier = get.Required("CoordinatorIdentifier", Decode.String),
			AllowP2wpkhInputs = get.Required("AllowP2wpkhInputs", Decode.Bool),
			AllowP2trInputs = get.Required("AllowP2trInputs", Decode.Bool),
			AllowP2wpkhOutputs = get.Required("AllowP2wpkhOutputs", Decode.Bool),
			AllowP2trOutputs = get.Required("AllowP2trOutputs", Decode.Bool),
			AllowP2pkhOutputs = get.Required("AllowP2pkhOutputs", Decode.Bool),
			AllowP2shOutputs = get.Required("AllowP2shOutputs", Decode.Bool),
			AllowP2wshOutputs = get.Required("AllowP2wshOutputs", Decode.Bool),
			DelayTransactionSigning = get.Required("DelayTransactionSigning", Decode.Bool),
			AnnouncerConfig = get.Required("AnnouncerConfig", AnnouncerConfig),
			PublishAsOnionService = get.Optional("PublishAsOnionService", Decode.Bool, true),
			OnionServicePrivateKey = get.Optional("OnionServicePrivateKey", Decode.String)
		});
}
