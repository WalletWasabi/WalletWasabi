using System.Linq;
using WabiSabi.Crypto.Randomness;

namespace WalletWasabi.CoinJoinProfiles;
public static class PrivacyProfiles
{
	public const int AbsoluteMinAnonScoreTarget = 2;
	public const int AbsoluteMaxAnonScoreTarget = 300;

	public static readonly IPrivacyProfile[] Profiles =
	[
		new Default(),
		new Economical(),
		new MaximizePrivacy()
	];

	public static readonly IPrivacyProfile DefaultProfile = Profiles.First(x => x.Name == "Default");

	public record Default : IPrivacyProfile
	{
		public int AnonScoreTarget => 10;
		public bool NonPrivateCoinIsolation => true;
	}

	public record Economical : IPrivacyProfile
	{
		public int AnonScoreTarget => 5;
		public bool NonPrivateCoinIsolation => false;
	}

	public record MaximizePrivacy : IPrivacyProfile
	{
		private const int MinExclusive = 29;
		private const int MaxExclusive = 51;

		public int AnonScoreTarget { get; } = GetAnonScoreTarget();
		public bool NonPrivateCoinIsolation => true;

		/// <summary>
		/// This algo linearly decreases the probability of increasing the anonset target, starting from minExclusive.
		/// The goal is to have a good distribution around a specific target with hard min and max.
		/// minExclusive has 100% chance of being skipped, maxExclusive has 0% chance (hard limit).
		/// Average of results is never more than minExclusive + (maxExclusive - minExclusive) * (1/3).
		/// </summary>
		public static int GetAnonScoreTarget()
		{
			var ast = MinExclusive + 1;

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
}
