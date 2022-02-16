using Avalonia.Media;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

public class ManualCoinJoinProfile : CoinJoinProfileViewModelBase
{
	public ManualCoinJoinProfile(int minAnonScoreTarget, int maxAnonScoreTarget, int feeRateAverageTimeFrameHours)
	{
		MinAnonScoreTarget = minAnonScoreTarget;
		MaxAnonScoreTarget = maxAnonScoreTarget;
		FeeRateAverageTimeFrameHours = feeRateAverageTimeFrameHours;
	}

	public override string Title => "Economy";

	public override string Description => "very Economy";

	public override IImage Icon => AssetHelpers.GetBitmapAsset($"avares://WalletWasabi.Fluent/Assets/WalletIcons/{ThemeHelper.CurrentTheme}/ledger.png");

	public override int MinAnonScoreTarget { get; }

	public override int MaxAnonScoreTarget { get; }

	public override int FeeRateAverageTimeFrameHours { get; }
}
