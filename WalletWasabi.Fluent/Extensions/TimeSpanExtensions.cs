using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.Extensions;

public static class TimeSpanExtensions
{
	public static TimeSpan Reduce(this TimeSpan input)
	{
		if (input.Days > 0)
		{
			return ReduceToDays(input);
		}

		if (input.Hours > 0)
		{
			return ReduceToHoursAndMinutes(input);
		}

		return ReduceToMinutes(input);
	}

	private static TimeSpan ReduceToDays(TimeSpan input)
	{
		if (input.Hours >= 12)
		{
			return TimeSpan.FromDays(input.Days + 1);
		}

		return TimeSpan.FromDays(input.Days);
	}

	private static TimeSpan ReduceToHoursAndMinutes(TimeSpan input)
	{
		return TimeSpan.FromHours(input.Hours).Add(TimeSpan.FromMinutes(MathUtils.Round(input.Minutes, 30)));
	}

	private static TimeSpan ReduceToMinutes(TimeSpan input)
	{
		return TimeSpan.FromMinutes(input.Minutes);
	}
}
