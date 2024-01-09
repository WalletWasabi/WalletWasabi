using System.Collections.Immutable;
namespace WalletWasabi.Extensions;

public static class DateTimeOffsetExtensions
{
	public static ImmutableList<DateTimeOffset> GetScheduledDates(this DateTimeOffset endTime, int howMany)
	{
		return endTime.GetScheduledDates(howMany, DateTimeOffset.UtcNow, TimeSpan.MaxValue);
	}

	public static ImmutableList<DateTimeOffset> GetScheduledDates(this DateTimeOffset endTime, int howMany, DateTimeOffset startTime, TimeSpan maximumRequestDelay)
	{
		var remainingTime = endTime - startTime;

		if (remainingTime > maximumRequestDelay)
		{
			remainingTime = maximumRequestDelay;
		}

		return remainingTime.SamplePoisson(howMany, startTime);
	}
}
