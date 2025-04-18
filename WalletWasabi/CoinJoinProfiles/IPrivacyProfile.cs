namespace WalletWasabi.CoinJoinProfiles;

public interface IPrivacyProfile
{
	string Name => GetType().Name;
	int AnonScoreTarget { get; }
	bool NonPrivateCoinIsolation { get; }
	public bool Equals(int anonScoreTarget, bool redCoinIsolation)
	{
		return anonScoreTarget == AnonScoreTarget && redCoinIsolation == NonPrivateCoinIsolation;
	}
}
