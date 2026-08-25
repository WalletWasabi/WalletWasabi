namespace WalletWasabi.CoinJoinProfiles;

public interface IPrivacyProfile
{
	string Name => GetType().Name;
	int AnonScoreTarget { get; }
	bool NonPrivateCoinIsolation { get; }
	bool OnlyUsePrivateFundsForPayments { get; }
	public bool Equals(int anonScoreTarget, bool redCoinIsolation, bool onlyUsePrivateFundsForPayments)
	{
		return anonScoreTarget == AnonScoreTarget
			&& redCoinIsolation == NonPrivateCoinIsolation
			&& onlyUsePrivateFundsForPayments == OnlyUsePrivateFundsForPayments;
	}
}
