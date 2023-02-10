using WalletWasabi.Logging;

namespace WalletWasabi.Affiliation.Models.CoinjoinRequest;

public record Input(Outpoint Prevout, byte[] ScriptPubkey, bool IsAffiliated, bool IsNoFee)
{
	public static Input FromAffiliateInput(AffiliateInput affiliateInput, string affiliationFlag)
	{
		var isAffiliated = affiliateInput.AffiliationFlag == affiliationFlag;
		if (affiliateInput.IsNoFee && isAffiliated)
		{
			Logger.LogWarning(
				$"Detected input with redundant affiliation flag: {affiliateInput.Prevout.Hash}, {affiliateInput.Prevout.N}");
		}

		return new(
			Outpoint.FromOutPoint(affiliateInput.Prevout),
			affiliateInput.ScriptPubKey.ToBytes(),
			isAffiliated,
			affiliateInput.IsNoFee);
	}
}
