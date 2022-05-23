namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

public class ManualCoinJoinProfileViewModel : CoinJoinProfileViewModelBase
{
	public ManualCoinJoinProfileViewModel(bool autoCoinjoin, int anonScoreTarget, int feeRateMedianTimeFrameHours)
	{
		AutoCoinjoin = autoCoinjoin;
		AnonScoreTarget = anonScoreTarget;
		FeeRateMedianTimeFrameHours = feeRateMedianTimeFrameHours;
	}

	public override string Title => "Manual";

	public override string Description => "";

	public override bool AutoCoinjoin { get; }

	public override int AnonScoreTarget { get; }

	public override int FeeRateMedianTimeFrameHours { get; }
}
