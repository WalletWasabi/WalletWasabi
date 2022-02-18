using Avalonia.Media;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

internal class PrivateCoinJoinProfile : CoinJoinProfileViewModelBase
{
	public override string Title => "Private";

	public override string[] Description => new[] { "Optimal privacy", "Fast", "Automatic CoinJoin" };

	public override IImage Icon => AssetHelpers.GetBitmapAsset($"avares://WalletWasabi.Fluent/Assets/WalletIcons/{ThemeHelper.CurrentTheme}/normal.png");
	public override int MinAnonScoreTarget => 50;
	public override int MaxAnonScoreTarget => 100;

	public override int FeeRateAverageTimeFrameHours => 0;
}
