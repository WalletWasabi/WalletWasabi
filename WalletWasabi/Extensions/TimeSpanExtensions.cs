using System.Collections.Immutable;
using System.Linq;
using WalletWasabi.Crypto.Randomness;

namespace System
{
	public static class TimeSpanExtensions
	{
		public static ImmutableList<DateTimeOffset> Sample(this TimeSpan timeFrame, int numberOfEvents)
		{
			var startTime = DateTimeOffset.UtcNow;
			using var random = new SecureRandom();
			return Enumerable
				.Range(0, numberOfEvents)
				.Select(_ => startTime + (0.8 * TimeSpan.FromMilliseconds(random.GetInt(0, (int)timeFrame.TotalMilliseconds))))
				.OrderBy(t => t)
				.ToImmutableList();
		}
	}
}