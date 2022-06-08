namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

public abstract class CoinJoinProfileViewModelBase : ViewModelBase
{
	public abstract string Title { get; }

	public abstract string Description { get; }

	public virtual int AnonScoreTarget { get; } = 5;

	public abstract int FeeRateMedianTimeFrameHours { get; }

	public override bool Equals(object? obj)
	{
		if (ReferenceEquals(this, obj))
		{
			return true;
		}

		if (obj is not CoinJoinProfileViewModelBase profile)
		{
			return false;
		}

		return profile.AnonScoreTarget == AnonScoreTarget && profile.FeeRateMedianTimeFrameHours == FeeRateMedianTimeFrameHours;
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(AnonScoreTarget, FeeRateMedianTimeFrameHours);
	}
}
