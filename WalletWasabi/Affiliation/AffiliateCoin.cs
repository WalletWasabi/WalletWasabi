using NBitcoin;

namespace WalletWasabi.Affiliation;

public class AffiliateCoin : Coin
{
	public AffiliateCoin(Coin coin, AffiliationFlag affiliationFlag, bool zeroCoordinationFee) : base(coin.Outpoint, coin.TxOut)
	{
		AffiliationFlag = affiliationFlag;
		ZeroCoordinationFee = zeroCoordinationFee;
	}

	public AffiliationFlag AffiliationFlag { get; }
	public bool ZeroCoordinationFee { get; }
}
