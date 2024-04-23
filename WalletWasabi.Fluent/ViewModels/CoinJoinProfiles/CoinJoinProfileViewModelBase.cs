using WalletWasabi.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

public abstract class CoinJoinProfileViewModelBase : ViewModelBase
{
	public abstract string Title { get; }

	public abstract string Description { get; }

	public virtual int AnonScoreTarget { get; } = 5;

	public virtual bool RedCoinIsolation { get; } = false;

	public virtual CoinjoinSkipFactors SkipFactors { get; } = CoinjoinSkipFactors.SpeedMaximizing;

	public virtual int FeeRateMedianTimeFrameHours { get; }

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
			&& profile.FeeRateMedianTimeFrameHours == FeeRateMedianTimeFrameHours
			&& profile.RedCoinIsolation == RedCoinIsolation
			&& profile.SkipFactors == SkipFactors;
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(AnonScoreTarget, FeeRateMedianTimeFrameHours, SkipFactors);
	}
}
