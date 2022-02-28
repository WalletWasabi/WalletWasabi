namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

public class ManualCoinJoinProfile : CoinJoinProfileViewModelBase
{
	public ManualCoinJoinProfile(bool autoCoinjoin, int minAnonScoreTarget, int maxAnonScoreTarget, int feeRateMedianTimeFrameHours)
	{
		AutoCoinjoin = autoCoinjoin;
		MinAnonScoreTarget = minAnonScoreTarget;
		MaxAnonScoreTarget = maxAnonScoreTarget;
		FeeRateMedianTimeFrameHours = feeRateMedianTimeFrameHours;
	}

	public override string Title => "Manual";

	public override string Description => "";

	public override bool AutoCoinjoin { get; }

	public override int MinAnonScoreTarget { get; }

	public override int MaxAnonScoreTarget { get; }

	public override int FeeRateMedianTimeFrameHours { get; }
}
