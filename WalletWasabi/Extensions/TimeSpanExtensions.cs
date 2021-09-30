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
				.Select(_ => startTime.Add(0.8 * random.NextDouble() * timeFrame))
				.OrderBy(t => t)
				.ToImmutableList();
		}
	}
}