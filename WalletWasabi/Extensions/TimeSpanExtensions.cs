using System.Collections.Immutable;
using System.Linq;
using WalletWasabi.Crypto.Randomness;

namespace System;

public static class TimeSpanExtensions
{
	public static ImmutableList<DateTimeOffset> SamplePoisson(this TimeSpan timeFrame, int numberOfEvents)
	{
		var startTime = DateTimeOffset.UtcNow;
		using var random = new SecureRandom();
		TimeSpan Sample(int milliseconds) =>
			milliseconds <= 0 ? TimeSpan.Zero : TimeSpan.FromMilliseconds(random.GetInt(0, milliseconds));

		return Enumerable
			.Range(0, numberOfEvents)
			.Select(_ => startTime + (0.8 * Sample((int)timeFrame.TotalMilliseconds)))
			.OrderBy(t => t)
			.ToImmutableList();
	}
}
