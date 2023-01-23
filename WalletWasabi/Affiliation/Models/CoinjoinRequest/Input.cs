using NBitcoin;
using Newtonsoft.Json;
using WalletWasabi.Affiliation.Serialization;

namespace WalletWasabi.Affiliation.Models.CoinjoinRequest;

public record Input
{
	public Input(Outpoint prevout, byte[] scriptPubkey, bool isAffiliated, bool isNoFee)
	{
		Prevout = prevout;
		ScriptPubkey = scriptPubkey;
		IsAffiliated = isAffiliated;
		IsNoFee = isNoFee;

		if (isNoFee && isAffiliated)
		{
			Logging.Logger.LogWarning($"Detected input with redundant affiliation flag: {Convert.ToHexString(prevout.Hash)}, {prevout.Index}");
		}
	}

	[JsonProperty(PropertyName = "prevout")]
	public Outpoint Prevout { get; }

	[JsonProperty(PropertyName = "script_pubkey")]
	[JsonConverter(typeof(AffiliationByteArrayJsonConverter))]
	public byte[] ScriptPubkey { get; }

	[JsonProperty(PropertyName = "is_affiliated")]
	public bool IsAffiliated { get; }

	[JsonProperty(PropertyName = "is_no_fee")]
	public bool IsNoFee { get; }

	public static Input FromAffiliateInput(AffiliateInput affiliateInput, AffiliationFlag affiliationFlag)
	{
		return new Input(Outpoint.FromOutPoint(affiliateInput.Prevout), affiliateInput.ScriptPubKey.ToBytes(), affiliateInput.AffiliationFlag == affiliationFlag, affiliateInput.IsNoFee);
	}
}
