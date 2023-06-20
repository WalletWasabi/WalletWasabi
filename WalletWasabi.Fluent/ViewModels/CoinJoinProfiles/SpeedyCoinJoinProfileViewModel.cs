namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

internal class SpeedyCoinJoinProfileViewModel : CoinJoinProfileViewModelBase
{
	public override string Title => "Maximize Speed";

	public override string Description => "Getting things done. Geared towards speed and convenience.";

	public override int FeeRateMedianTimeFrameHours => 0;
	public override double CoinjoinProbabilityDaily { get; } = 0.7;
	public override double CoinjoinProbabilityWeekly { get; } = 0.8;
	public override double CoinjoinProbabilityMonthly { get; } = 0.9;
}
