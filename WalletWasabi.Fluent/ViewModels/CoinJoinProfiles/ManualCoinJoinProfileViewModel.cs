namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

public class ManualCoinJoinProfileViewModel : CoinJoinProfileViewModelBase
{
	public ManualCoinJoinProfileViewModel(bool autoStartCoinjoin, int anonScoreTarget, int feeRateMedianTimeFrameHours)
	{
		AutoStartCoinjoin = autoStartCoinjoin;
		AnonScoreTarget = anonScoreTarget;
		FeeRateMedianTimeFrameHours = feeRateMedianTimeFrameHours;
	}

	public override string Title => "Manual";

	public override string Description => "";

	public override bool AutoStartCoinjoin { get; }

	public override int AnonScoreTarget { get; }

	public override int FeeRateMedianTimeFrameHours { get; }
}
