using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

public class ManualCoinJoinProfile : CoinJoinProfileViewModelBase
{
	public ManualCoinJoinProfile(int minAnonScoreTarget, int maxAnonScoreTarget, int feeTargetAverageTimeFrameHours)
	{
		MinAnonScoreTarget = minAnonScoreTarget;
		MaxAnonScoreTarget = maxAnonScoreTarget;
		FeeTargetAverageTimeFrameHours = feeTargetAverageTimeFrameHours;
	}

	public override string Title => "Economy";

	public override string Description => "very Economy";

	public override IImage Icon => AssetHelpers.GetBitmapAsset($"avares://WalletWasabi.Fluent/Assets/WalletIcons/{ThemeHelper.CurrentTheme}/ledger.png");

	public override int MinAnonScoreTarget { get; }

	public override int MaxAnonScoreTarget { get; }

	public override int FeeTargetAverageTimeFrameHours { get; }
}
