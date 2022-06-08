using WalletWasabi.Blockchain.Keys;

namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

public class ManualCoinJoinProfileViewModel : CoinJoinProfileViewModelBase
{
	public ManualCoinJoinProfileViewModel(int anonScoreTarget, int feeRateMedianTimeFrameHours)
	{
		AnonScoreTarget = anonScoreTarget;
		FeeRateMedianTimeFrameHours = feeRateMedianTimeFrameHours;
	}

	public ManualCoinJoinProfileViewModel(KeyManager keyManager) : this(keyManager.AnonScoreTarget, keyManager.FeeRateMedianTimeFrameHours)
	{
	}

	public override string Title => "Custom";

	public override string Description => "";

	public override int AnonScoreTarget { get; }

	public override int FeeRateMedianTimeFrameHours { get; }
}
