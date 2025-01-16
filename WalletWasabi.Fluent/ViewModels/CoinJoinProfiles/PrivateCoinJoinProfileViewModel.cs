using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

// TODO: All of this should be moved outside the Fluent project.
public class PrivateCoinJoinProfileViewModel : CoinJoinProfileViewModelBase
{
	// TODO: Safety coinjoins should be moved here & be configurable.
	public const int MinAnonScore = 30;
	public const int MaxAnonScore = 50;

	public PrivateCoinJoinProfileViewModel(int anonScoreTarget)
	{
		AnonScoreTarget = anonScoreTarget;
	}

	public PrivateCoinJoinProfileViewModel()
	{
		AnonScoreTarget = GetAnonScoreTarget(MinAnonScore, MaxAnonScore);
	}

	public override string Title => Lang.Resources.PrivateCoinJoinProfileViewModel_Title;
	public override string Description => Lang.Resources.PrivateCoinJoinProfileViewModel_Description;

	public override int AnonScoreTarget { get; }
	public override bool RedCoinIsolation { get; } = true;

	public override CoinjoinSkipFactors SkipFactors { get; } = CoinjoinSkipFactors.PrivacyMaximizing;

	public override int FeeRateMedianTimeFrameHours => 0;

	/// <summary>
	/// This algo linearly decreases the probability of increasing the anonset target, starting from minExclusive.
	/// The goal is to have a good distribution around a specific target with hard min and max.
	/// (minExclusive + 1) has 100% chance of being selected, (maxExclusive) has a 0% chance (hard limit).
	/// Average of results is never more than minExclusive + (maxExclusive - minExclusive) * (1.0/3.0).
	/// </summary>
	public static int GetAnonScoreTarget(int minExclusive, int maxExclusive)
	{
		var ast = minExclusive;

		while (ast < maxExclusive)
		{
			var progress = (double)(ast - minExclusive) / (maxExclusive - minExclusive);
			var probability = 100 * (1 - progress);

			if (SecureRandom.Instance.GetInt(0, 101) > probability)
			{
				break;
			}

			ast++;
		}

		return ast;
	}

	// This function is badly designed and creates problems with retro-compatibility.
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
			&& profile.FeeRateMedianTimeFrameHours == FeeRateMedianTimeFrameHours
			&& profile.RedCoinIsolation == RedCoinIsolation
			&& profile.SkipFactors == SkipFactors;
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(AnonScoreTarget, FeeRateMedianTimeFrameHours, RedCoinIsolation, SkipFactors);
	}
}
