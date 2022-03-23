using System.Linq;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.Helpers;

public static class TimeSpanFormatter
{
	public static string Format(TimeSpan timeSpan, Configuration configuration)
	{
		var reduced = timeSpan.Reduce();

		var parts = new[]
		{
			GetDays(reduced, configuration),
			GetHours(reduced, configuration),
			GetMinutes(reduced, configuration)
		};
			
		return parts.First(s => s is not null) ?? throw new InvalidOperationException($"Invalid timeSpan: {timeSpan}");
	}

	private static string? GetDays(TimeSpan timeSpan, Configuration configuration)
	{
		if (timeSpan.Days > 0)
		{
			return timeSpan.Days + configuration.DaysLabel;
		}

		return null;
	}

	private static string? GetHours(TimeSpan timeSpan, Configuration configuration)
	{
		if (timeSpan.Hours > 0)
		{
			return timeSpan.Hours + configuration.HoursLabel;
		}

		return null;
	}

	private static string? GetMinutes(TimeSpan timeSpan, Configuration configuration)
	{
		if (timeSpan.Minutes > 0)
		{
			return timeSpan.Minutes + configuration.MinutesLabel;
		}

		return default;
	}

	public class Configuration
	{
		public string DaysLabel { get; }
		public string HoursLabel { get; }
		public string MinutesLabel { get; }

		public Configuration(string daysLabel, string hoursLabel, string minutesLabel)
		{
			DaysLabel = daysLabel;
			HoursLabel = hoursLabel;
			MinutesLabel = minutesLabel;
		}
	}
}