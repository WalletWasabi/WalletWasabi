using static WalletWasabi.CoinJoinProfiles.CoinJoinTimeFrames;

namespace WalletWasabi.CoinJoinProfiles;
public interface IPrivacyProfile
{
	string Name => GetType().Name;
	int AnonScoreTarget { get; }
	bool RedCoinIsolation { get; }
	TimeFrameItem TimeFrame { get; }
	public bool Equals(int anonScoreTarget, bool redCoinIsolation, TimeSpan timeFrame)
	{
		return anonScoreTarget == AnonScoreTarget && redCoinIsolation == RedCoinIsolation && timeFrame == TimeFrame.TimeFrame;
	}
}
