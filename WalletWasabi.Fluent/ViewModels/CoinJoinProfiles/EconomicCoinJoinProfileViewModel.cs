using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

internal class EconomicCoinJoinProfileViewModel : CoinJoinProfileViewModelBase
{
	public override string Title => Lang.Resources.EconomicCoinJoinProfileViewModel_Title;
	public override string Description => Lang.Resources.EconomicCoinJoinProfileViewModel_Description;

	public override int FeeRateMedianTimeFrameHours => 168; // One week median.

	public override CoinjoinSkipFactors SkipFactors { get; } = CoinjoinSkipFactors.CostMinimizing;
}
