using NBitcoin;

namespace WalletWasabi.WabiSabiClientLibrary.Models.GetAnonymityScores;

public record ExternalOutput(Money Value, string ScriptPubKey)
{
}
