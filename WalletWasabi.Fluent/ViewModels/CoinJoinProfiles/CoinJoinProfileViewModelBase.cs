using WalletWasabi.Crypto.Randomness;

namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

public abstract class CoinJoinProfileViewModelBase : ViewModelBase
{
	public abstract string Title { get; }

	public abstract string Description { get; }

	public virtual int AnonScoreTarget { get; }

	public virtual bool RedCoinIsolation { get; } = false;

	public virtual int FeeRateMedianTimeFrameHours { get; }

	protected static int GetRandom(int minInclusive, int maxExclusive)
	{
		return SecureRandom.Instance.GetInt(minInclusive, maxExclusive);
	}

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

		return profile.AnonScoreTarget == AnonScoreTarget && profile.FeeRateMedianTimeFrameHours == FeeRateMedianTimeFrameHours && profile.RedCoinIsolation == RedCoinIsolation;
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(AnonScoreTarget, FeeRateMedianTimeFrameHours);
	}
}
