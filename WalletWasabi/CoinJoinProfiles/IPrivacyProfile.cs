namespace WalletWasabi.CoinJoinProfiles;

public interface IPrivacyProfile
{
	string Name => GetType().Name;
	int AnonScoreTarget { get; }
	bool NonPrivateCoinIsolation { get; }
	bool AllowPaymentsRegardlessOfAnonScore { get; }
	public bool Equals(int anonScoreTarget, bool redCoinIsolation, bool allowPaymentsRegardlessOfAnonScore)
	{
		return anonScoreTarget == AnonScoreTarget
			&& redCoinIsolation == NonPrivateCoinIsolation
			&& allowPaymentsRegardlessOfAnonScore == AllowPaymentsRegardlessOfAnonScore;
	}
}
