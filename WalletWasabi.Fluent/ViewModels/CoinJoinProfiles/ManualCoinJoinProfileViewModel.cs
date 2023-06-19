using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

public class ManualCoinJoinProfileViewModel : CoinJoinProfileViewModelBase
{
	public ManualCoinJoinProfileViewModel(
		int anonScoreTarget,
		bool redCoinIsolation,
		double coinjoinProbabilityDaily,
		double coinjoinProbabilityWeekly,
		double coinjoinProbabilityMonthly)
	{
		AnonScoreTarget = anonScoreTarget;
		RedCoinIsolation = redCoinIsolation;
		CoinjoinProbabilityDaily = coinjoinProbabilityDaily;
		CoinjoinProbabilityWeekly = coinjoinProbabilityWeekly;
		CoinjoinProbabilityMonthly = coinjoinProbabilityMonthly;
	}

	public ManualCoinJoinProfileViewModel(IWalletSettingsModel walletSettings)
		: this(
			  walletSettings.AnonScoreTarget,
			  walletSettings.RedCoinIsolation,
			  walletSettings.CoinjoinProbabilityDaily,
			  walletSettings.CoinjoinProbabilityWeekly,
			  walletSettings.CoinjoinProbabilityMonthly)
	{
	}

	public override string Title => "Custom";

	public override string Description => "";

	public override int AnonScoreTarget { get; }

	public override bool RedCoinIsolation { get; }
	public override double CoinjoinProbabilityDaily { get; }
	public override double CoinjoinProbabilityWeekly { get; }
	public override double CoinjoinProbabilityMonthly { get; }
}
