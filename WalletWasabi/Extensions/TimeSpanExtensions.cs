using System.Collections.Immutable;
using System.Linq;
using WalletWasabi.Crypto.Randomness;

namespace WalletWasabi.Extensions;

public static class TimeSpanExtensions
{
	public static ImmutableList<DateTimeOffset> SamplePoisson(this TimeSpan timeFrame, int numberOfEvents, DateTimeOffset startTime)
	{
		return timeFrame.SamplePoissonDelays(numberOfEvents).Select(delay => startTime + delay).ToImmutableList();
	}

	public static ImmutableList<TimeSpan> SamplePoissonDelays(this TimeSpan timeFrame, int numberOfEvents)
	{
		static TimeSpan Sample(int milliseconds) =>
			milliseconds <= 0 ? TimeSpan.Zero : TimeSpan.FromMilliseconds(SecureRandom.Instance.GetInt(0, milliseconds));

		return Enumerable
			.Range(0, numberOfEvents)
			.Select(_ => 0.8 * Sample((int)timeFrame.TotalMilliseconds))
			.OrderBy(t => t)
			.ToImmutableList();
	}
}
