using NBitcoin;
using Newtonsoft.Json;
using WalletWasabi.Affiliation.Serialization;

namespace WalletWasabi.Affiliation.Models.CoinjoinRequest;

public record Input
{
	public Input(Outpoint prevout, byte[] scriptPubkey, bool isNoFee, bool isAffiliated)
	{
		Prevout = prevout;
		ScriptPubkey = scriptPubkey;
		IsNoFee = isNoFee;

		if (isNoFee && isAffiliated)
		{
			Logging.Logger.LogWarning($"Detected input with redundant affiliation flag: {Convert.ToHexString(prevout.Hash)}, {prevout.Index}");
		}

		IsAffiliated = isAffiliated;
	}

	[JsonProperty(PropertyName = "prevout")]
	public Outpoint Prevout { get; }

	[JsonProperty(PropertyName = "script_pubkey")]
	[JsonConverter(typeof(AffiliationByteArrayJsonConverter))]
	public byte[] ScriptPubkey { get; }

	[JsonProperty(PropertyName = "is_no_fee")]
	public bool IsNoFee { get; }

	[JsonProperty(PropertyName = "is_affiliated")]
	public bool IsAffiliated { get; }

	public static Input FromCoin(Coin coin, bool is_no_fee, bool is_affiliated)
	{
		return new Input(Outpoint.FromOutpoint(coin.Outpoint), coin.ScriptPubKey.ToBytes(), is_no_fee, is_affiliated);
	}
}
