using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

public class ManualCoinJoinProfileViewModel : CoinJoinProfileViewModelBase
{
	public ManualCoinJoinProfileViewModel(
		int anonScoreTarget,
		int feeRateMedianTimeFrameHours,
		bool redCoinIsolation,
		CoinjoinSkipFactors skipFactors)
	{
		AnonScoreTarget = anonScoreTarget;
		FeeRateMedianTimeFrameHours = feeRateMedianTimeFrameHours;
		RedCoinIsolation = redCoinIsolation;
		SkipFactors = skipFactors;
	}

	public ManualCoinJoinProfileViewModel(IWalletSettingsModel walletSettings)
		: this(
			  walletSettings.AnonScoreTarget,
			  walletSettings.FeeRateMedianTimeFrameHours,
			  walletSettings.RedCoinIsolation,
			  walletSettings.CoinjoinSkipFactors)
	{
	}

	public override string Title => "Custom";

	public override string Description => "";

	public override int AnonScoreTarget { get; }

	public override int FeeRateMedianTimeFrameHours { get; }
	public override bool RedCoinIsolation { get; }
	public override CoinjoinSkipFactors SkipFactors { get; }
}
