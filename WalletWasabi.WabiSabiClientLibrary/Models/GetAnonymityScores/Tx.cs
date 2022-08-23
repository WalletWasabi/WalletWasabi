using NBitcoin;

namespace WalletWasabi.WabiSabiClientLibrary.Models.GetAnonymityScores;

public record Tx(
	InternalInput[] InternalInputs,
	InternalOutput[] InternalOutputs,
	ExternalInput[] ExternalInputs,
	ExternalOutput[] ExternalOutputs
);
