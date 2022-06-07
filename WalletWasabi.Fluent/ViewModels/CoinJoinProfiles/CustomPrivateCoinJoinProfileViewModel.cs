namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

internal class CustomPrivateCoinJoinProfileViewModel : PrivateCoinJoinProfileViewModel
{
	private readonly int _anonScoreTarget;
	private readonly int _feeRateMedianTimeFrameHours;

	public CustomPrivateCoinJoinProfileViewModel(int anonScoreTarget, int feeRateMedianTimeFrameHours)
	{
		_anonScoreTarget = anonScoreTarget;
		_feeRateMedianTimeFrameHours = feeRateMedianTimeFrameHours;
	}

	public override int AnonScoreTarget => _anonScoreTarget;
	public override int FeeRateMedianTimeFrameHours => _feeRateMedianTimeFrameHours;
}
