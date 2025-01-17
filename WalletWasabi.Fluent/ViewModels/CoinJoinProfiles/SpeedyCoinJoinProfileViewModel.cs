using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

internal class SpeedyCoinJoinProfileViewModel : CoinJoinProfileViewModelBase
{
	public override string Title => Lang.Resources.SpeedyCoinJoinProfile_Title;
	public override string Description => Lang.Resources.SpeedyCoinJoinProfile_Description;

	public override int FeeRateMedianTimeFrameHours => 0;

	public override CoinjoinSkipFactors SkipFactors { get; } = CoinjoinSkipFactors.SpeedMaximizing;
}
