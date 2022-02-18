using Avalonia.Media;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

internal class EconomyCoinJoinProfile : CoinJoinProfileViewModelBase
{
	public override string Title => "Economy";

	public override string[] Description => new[] { "Sufficient privacy", "Slow but cheaper", "Automatic CoinJoin" };

	public override IImage Icon => AssetHelpers.GetBitmapAsset($"avares://WalletWasabi.Fluent/Assets/WalletIcons/{ThemeHelper.CurrentTheme}/normal.png");

	public override int FeeRateAverageTimeFrameHours => 168; // One week average.
}
