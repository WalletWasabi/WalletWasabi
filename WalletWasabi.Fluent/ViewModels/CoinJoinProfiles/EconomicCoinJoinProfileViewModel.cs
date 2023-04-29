namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

internal class EconomicCoinJoinProfileViewModel : CoinJoinProfileViewModelBase
{
	public const int MinAnonScore = 5;
	public const int MaxAnonScore = 10;

	public EconomicCoinJoinProfileViewModel(int anonScoreTarget)
	{
		AnonScoreTarget = anonScoreTarget;
	}

	public EconomicCoinJoinProfileViewModel()
	{
		AnonScoreTarget = GetRandom(MinAnonScore, MaxAnonScore);
	}

	public override string Title => "Minimize Costs";

	public override string Description => "For savers. Only participates in coinjoins during the cheapest parts of the week.";

	public override int FeeRateMedianTimeFrameHours => 168; // One week median.
	public override int AnonScoreTarget { get; }
}
