using NBitcoin;

namespace WalletWasabi.Affiliation.Models.CoinJoinNotification;

public record Output(long Amount, byte[] ScriptPubkey)
{
	public static Output FromTxOut(TxOut txOut) =>
		new(txOut.Value, txOut.ScriptPubKey.ToBytes());
}
