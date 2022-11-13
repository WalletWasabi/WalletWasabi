using WalletWasabi.Blockchain.Keys;

namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

public class ManualCoinJoinProfileViewModel : CoinJoinProfileViewModelBase
{
	public ManualCoinJoinProfileViewModel(int anonScoreTarget, int feeRateMedianTimeFrameHours, bool redCoinIsolation)
	{
		AnonScoreTarget = anonScoreTarget;
		FeeRateMedianTimeFrameHours = feeRateMedianTimeFrameHours;
		RedCoinIsolation = redCoinIsolation;
	}

	public ManualCoinJoinProfileViewModel(KeyManager keyManager) : this(keyManager.AnonScoreTarget, keyManager.FeeRateMedianTimeFrameHours, keyManager.RedCoinIsolation)
	{
	}

	public override string Title => "Custom";

	public override string Description => "";

	public override int AnonScoreTarget { get; }

	public override int FeeRateMedianTimeFrameHours { get; }
	public override bool RedCoinIsolation { get; }
}
