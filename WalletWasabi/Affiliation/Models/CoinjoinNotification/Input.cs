using WalletWasabi.Logging;

namespace WalletWasabi.Affiliation.Models.CoinJoinNotification;

public record Input(Outpoint Prevout, byte[] ScriptPubkey, bool IsAffiliated, bool IsNoFee)
{
	public static Input FromAffiliateInput(AffiliateInput affiliateInput, string affiliationId)
	{
		var isAffiliated = affiliateInput.AffiliationId == affiliationId;
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
