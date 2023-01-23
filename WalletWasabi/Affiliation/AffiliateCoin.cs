using NBitcoin;

namespace WalletWasabi.Affiliation;

public class AffiliateCoin : Coin
{
	public AffiliateCoin(Coin coin, AffiliationFlag affiliationFlag, bool isNoFee) : base(coin.Outpoint, coin.TxOut)
	{
		AffiliationFlag = affiliationFlag;
		IsNoFee = isNoFee;
	}

	public AffiliationFlag AffiliationFlag { get; }
	public bool IsNoFee { get; }
}
