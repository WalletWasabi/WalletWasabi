using NBitcoin;

namespace WalletWasabi.Affiliation;

public record AffiliateInput
{
	public AffiliateInput(OutPoint prevout, Script scriptPubKey, AffiliationFlag affiliationFlag, bool isNoFee)
	{
		Prevout = prevout;
		ScriptPubKey = scriptPubKey;
		AffiliationFlag = affiliationFlag;
		IsNoFee = isNoFee;
	}

	public AffiliateInput(Coin coin, AffiliationFlag affiliationFlag, bool isNoFee)
		  : this(coin.Outpoint, coin.ScriptPubKey, affiliationFlag, isNoFee)
	{
	}

	public OutPoint Prevout { get; }
	public Script ScriptPubKey { get; }
	public AffiliationFlag AffiliationFlag { get; }
	public bool IsNoFee { get; }
}
