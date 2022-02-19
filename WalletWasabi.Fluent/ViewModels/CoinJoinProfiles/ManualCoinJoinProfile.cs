using Avalonia.Media;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

public class ManualCoinJoinProfile : CoinJoinProfileViewModelBase
{
	public ManualCoinJoinProfile(bool autoCoinjoin, int minAnonScoreTarget, int maxAnonScoreTarget, int feeRateAverageTimeFrameHours)
	{
		AutoCoinjoin = autoCoinjoin;
		MinAnonScoreTarget = minAnonScoreTarget;
		MaxAnonScoreTarget = maxAnonScoreTarget;
		FeeRateAverageTimeFrameHours = feeRateAverageTimeFrameHours;
	}

	public override string Title => "Manual";

	public override string Description => "";

	public override IImage Icon => AssetHelpers.GetBitmapAsset($"avares://WalletWasabi.Fluent/Assets/WalletIcons/{ThemeHelper.CurrentTheme}/normal.png");

	public override bool AutoCoinjoin { get; }
	public override int MinAnonScoreTarget { get; }

	public override int MaxAnonScoreTarget { get; }

	public override int FeeRateAverageTimeFrameHours { get; }
}
