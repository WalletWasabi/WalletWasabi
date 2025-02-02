using WabiSabi.Crypto.Randomness;

namespace WalletWasabi.Helpers;
public static class DefaultAnonScoreTargetHelper
{

	public const int MinExclusive = 30;
	public const int MaxExclusive = 51;

	/// <summary>
	/// This algo linearly decreases the probability of increasing the anonset target, starting from minExclusive.
	/// The goal is to have a good distribution around a specific target with hard min and max.
	/// (minExclusive + 1) has 100% chance of being selected, (maxExclusive) has a 0% chance (hard limit).
	/// Average of results is never more than minExclusive + (maxExclusive - minExclusive) * (1.0/3.0).
	/// </summary>
	public static int GetDefaultAnonScoreTarget()
	{

		var ast = MinExclusive;

		while (ast < MaxExclusive)
		{
			var progress = (double)(ast - MinExclusive) / (MaxExclusive - MinExclusive);
			var probability = 100 * (1 - progress);

			if (SecureRandom.Instance.GetInt(0, 101) > probability)
			{
				break;
			}

			ast++;
		}

		return ast;
	}
}
