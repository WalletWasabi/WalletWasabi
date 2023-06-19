namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

public abstract class CoinJoinProfileViewModelBase : ViewModelBase
{
	public abstract string Title { get; }

	public abstract string Description { get; }

	public virtual int AnonScoreTarget { get; } = 5;

	public virtual bool RedCoinIsolation { get; } = false;

	public virtual double CoinjoinProbabilityDaily { get; } = 0.7;
	public virtual double CoinjoinProbabilityWeekly { get; } = 0.8;
	public virtual double CoinjoinProbabilityMonthly { get; } = 0.9;

	public static bool operator ==(CoinJoinProfileViewModelBase x, CoinJoinProfileViewModelBase y)
	{
		if (x is null)
		{
			if (y is null)
			{
				return true;
			}

			return false;
		}

		return x.Equals(y);
	}

	public static bool operator !=(CoinJoinProfileViewModelBase x, CoinJoinProfileViewModelBase y) => !(x == y);

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

		return profile.AnonScoreTarget == AnonScoreTarget
			&& profile.RedCoinIsolation == RedCoinIsolation
			&& profile.CoinjoinProbabilityDaily == CoinjoinProbabilityDaily
			&& profile.CoinjoinProbabilityWeekly == CoinjoinProbabilityWeekly
			&& profile.CoinjoinProbabilityMonthly == CoinjoinProbabilityMonthly;
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(AnonScoreTarget, CoinjoinProbabilityDaily, CoinjoinProbabilityWeekly, CoinjoinProbabilityMonthly);
	}
}
