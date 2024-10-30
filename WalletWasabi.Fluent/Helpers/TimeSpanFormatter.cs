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
			return $"{timeSpan.Days} {(configuration.AddSpace ? " " : "")}{Lang.Utils.PluralIfNeeded(timeSpan.Days, "Words_Day")}";
		}

		return null;
	}

	private static string? GetHours(TimeSpan timeSpan, Configuration configuration)
	{
		if (timeSpan.Hours > 0)
		{
			return $"{timeSpan.Hours} {(configuration.AddSpace ? " " : "")}{Lang.Utils.PluralIfNeeded(timeSpan.Hours, "Words_Hour")}";
		}

		return null;
	}

	private static string? GetMinutes(TimeSpan timeSpan, Configuration configuration)
	{
		if (timeSpan.Minutes > 0)
		{
			return $"{timeSpan.Minutes} {(configuration.AddSpace ? " " : "")}{Lang.Utils.PluralIfNeeded(timeSpan.Minutes, "Words_Minute")}";
		}

		return default;
	}

	public record Configuration(bool AddSpace);
}
