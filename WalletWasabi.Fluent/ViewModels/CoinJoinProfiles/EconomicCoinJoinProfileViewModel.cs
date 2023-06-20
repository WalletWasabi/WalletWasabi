namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

internal class EconomicCoinJoinProfileViewModel : CoinJoinProfileViewModelBase
{
	public override string Title => "Minimize Costs";

	public override string Description => "For savers. Only participates in coinjoins during the cheapest parts of the week.";

	public override int FeeRateMedianTimeFrameHours => 168; // One week median.
	public override double CoinjoinProbabilityDaily { get; } = 0.1;
	public override double CoinjoinProbabilityWeekly { get; } = 0.2;
	public override double CoinjoinProbabilityMonthly { get; } = 0.3;
}
