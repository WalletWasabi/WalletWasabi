using Avalonia.Media;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

internal class SpeedyCoinJoinProfile : CoinJoinProfileViewModelBase
{
	public override string Title => "Speedy";

	public override string Description => "very speedy";

	public override IImage Icon => AssetHelpers.GetBitmapAsset($"avares://WalletWasabi.Fluent/Assets/WalletIcons/{ThemeHelper.CurrentTheme}/trezor.png");

	public override int FeeRateAverageTimeFrameHours => 0;
}
