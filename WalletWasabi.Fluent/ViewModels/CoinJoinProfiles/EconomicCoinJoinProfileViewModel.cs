using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

internal class EconomicCoinJoinProfileViewModel : CoinJoinProfileViewModelBase
{
	public override string Title => "Minimize Costs";

	public override string Description => "For savers. Only participates in coinjoins during the cheapest parts of the week.";

	public override int FeeRateMedianTimeFrameHours => 168; // One week median.
}
