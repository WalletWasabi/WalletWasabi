using NBitcoin;
using Newtonsoft.Json;
using WalletWasabi.Affiliation.Serialization;

namespace WalletWasabi.Affiliation.Models.CoinjoinRequest;

public record Output
{
	public Output(long amount, byte[] script_pubkey)
	{
		Amount = amount;
		ScriptPubkey = script_pubkey;
	}

	[JsonProperty(PropertyName = "amount")]
	public long Amount { get; }

	[JsonProperty(PropertyName = "script_pubkey")]
	[JsonConverter(typeof(AffiliationByteArrayJsonConverter))]
	public byte[] ScriptPubkey { get; }

	public static Output FromTxOut(TxOut txOut)
	{
		return new Output(txOut.Value, txOut.ScriptPubKey.ToBytes());
	}
}
