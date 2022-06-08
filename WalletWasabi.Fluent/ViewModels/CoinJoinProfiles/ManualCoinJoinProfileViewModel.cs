namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

public class ManualCoinJoinProfileViewModel : CoinJoinProfileViewModelBase
{
	public ManualCoinJoinProfileViewModel(int anonScoreTarget, int feeRateMedianTimeFrameHours)
	{
		AnonScoreTarget = anonScoreTarget;
		FeeRateMedianTimeFrameHours = feeRateMedianTimeFrameHours;
	}

	public override string Title => "Custom";

	public override string Description => "";

	public override int AnonScoreTarget { get; }

	public override int FeeRateMedianTimeFrameHours { get; }
}
