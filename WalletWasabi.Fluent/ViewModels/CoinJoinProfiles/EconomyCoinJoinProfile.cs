using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

internal class EconomyCoinJoinProfile : CoinJoinProfileViewModelBase
{
	public override string Title => "Economy";

	public override string Description => "very Economy";

	public override IImage Icon => AssetHelpers.GetBitmapAsset($"avares://WalletWasabi.Fluent/Assets/WalletIcons/{ThemeHelper.CurrentTheme}/ledger.png");

	public override int FeeTargetAverageTimeFrameHours => 168; // One week avarage.
}