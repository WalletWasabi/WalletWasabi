namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

internal class SpeedyCoinJoinProfileViewModel : CoinJoinProfileViewModelBase
{
	public override string Title => "Maximize Speed (Default)";

	public override string Description => "Getting things done. Geared towards speed and convenience.";

	public override int FeeRateMedianTimeFrameHours => 0;
}
