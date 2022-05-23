namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

public abstract class CoinJoinProfileViewModelBase : ViewModelBase
{
	public abstract string Title { get; }

	public abstract string Description { get; }

	public virtual bool AutoStartCoinjoin { get; } = true;

	public virtual int AnonScoreTarget { get; } = 5;

	public abstract int FeeRateMedianTimeFrameHours { get; }
}
