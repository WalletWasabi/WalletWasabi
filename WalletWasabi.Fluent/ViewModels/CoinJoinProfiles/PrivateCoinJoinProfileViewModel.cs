using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

internal class PrivateCoinJoinProfileViewModel : CoinJoinProfileViewModelBase
{
	// https://github.com/WalletWasabi/WalletWasabi/pull/10468#issuecomment-1506284198
	public const int MinAnonScore = 27;

	public const int MaxAnonScore = 76;

	public PrivateCoinJoinProfileViewModel(int anonScoreTarget)
	{
		AnonScoreTarget = anonScoreTarget;
	}

	public PrivateCoinJoinProfileViewModel()
	{
		AnonScoreTarget = GetRandom(MinAnonScore, MaxAnonScore);
	}

	public override string Title => "Maximize Privacy";

	public override string Description => "Choice of the paranoid. Optimizes for privacy at all costs.";

	public override int AnonScoreTarget { get; }
	public override bool RedCoinIsolation { get; } = true;

	private static int GetRandom(int minInclusive, int maxExclusive)
	{
		return SecureRandom.Instance.GetInt(minInclusive, maxExclusive);
	}

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

		return profile.AnonScoreTarget < MaxAnonScore
		       && profile.AnonScoreTarget >= MinAnonScore
		       && profile.RedCoinIsolation == RedCoinIsolation;
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(AnonScoreTarget, RedCoinIsolation);
	}
}
