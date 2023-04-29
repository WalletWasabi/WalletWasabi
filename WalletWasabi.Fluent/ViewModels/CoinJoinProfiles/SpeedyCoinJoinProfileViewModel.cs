namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

internal class SpeedyCoinJoinProfileViewModel : CoinJoinProfileViewModelBase
{
	public const int MinAnonScore = 5;
	public const int MaxAnonScore = 10;

	public SpeedyCoinJoinProfileViewModel(int anonScoreTarget)
	{
		AnonScoreTarget = anonScoreTarget;
	}

	public SpeedyCoinJoinProfileViewModel()
	{
		AnonScoreTarget = GetRandom(MinAnonScore, MaxAnonScore + 1);
	}

	public override string Title => "Maximize Speed";

	public override string Description => "Getting things done. Geared towards speed and convenience.";

	public override int FeeRateMedianTimeFrameHours => 0;
	public override int AnonScoreTarget { get; }
}
