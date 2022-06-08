using WalletWasabi.Crypto.Randomness;

namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

internal class PrivateCoinJoinProfileViewModel : CoinJoinProfileViewModelBase
{
	public const int MinAnonScore = 50;
	public const int MaxAnonScore = 101;

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

	public override int FeeRateMedianTimeFrameHours => 0;

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

		return profile.AnonScoreTarget < MaxAnonScore && profile.AnonScoreTarget > MinAnonScore && profile.FeeRateMedianTimeFrameHours == FeeRateMedianTimeFrameHours;
	}
}
