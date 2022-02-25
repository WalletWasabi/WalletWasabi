using Avalonia.Media;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

internal class EconomyCoinJoinProfile : CoinJoinProfileViewModelBase
{
	public override string Title => "Minimize Costs";

	public override string Description => "For savers. Only participates in coinjoins during the cheapest parts of the week.";

	public override IImage Icon => AssetHelpers.GetBitmapAsset($"avares://WalletWasabi.Fluent/Assets/WalletIcons/{ThemeHelper.CurrentTheme}/normal.png");

	public override int FeeRateMedianTimeFrameHours => 168; // One week median.
}
