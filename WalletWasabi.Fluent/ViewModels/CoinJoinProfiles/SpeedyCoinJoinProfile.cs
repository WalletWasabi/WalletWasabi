using Avalonia.Media;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

internal class SpeedyCoinJoinProfile : CoinJoinProfileViewModelBase
{
	public override string Title => "Maximize Speed";

	public override string Description => "Getting things done. Geared towards speed and convenience.";

	public override IImage Icon => AssetHelpers.GetBitmapAsset($"avares://WalletWasabi.Fluent/Assets/WalletIcons/{ThemeHelper.CurrentTheme}/normal.png");

	public override int FeeRateAverageTimeFrameHours => 0;
}
