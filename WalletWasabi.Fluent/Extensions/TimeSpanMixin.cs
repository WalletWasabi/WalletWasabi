namespace WalletWasabi.Fluent.Extensions;

public static class TimeSpanMixin
{
	public static TimeSpan Reduce(this TimeSpan input)
	{
		if (input.Days > 0)
		{
			return ReduceToDays(input);
		}

		if (input.Hours > 0)
		{
			return ReduceToHours(input);
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

	private static TimeSpan ReduceToHours(TimeSpan input)
	{
		if (input.Hours >= 12)
		{
			return TimeSpan.FromDays(input.Days + 1);
		}

		if (input.Minutes >= 30)
		{
			return TimeSpan.FromHours(input.Hours + 1);
		}

		return TimeSpan.FromHours(input.Hours);
	}

	private static TimeSpan ReduceToMinutes(TimeSpan input)
	{
		return TimeSpan.FromMinutes(input.Minutes);
	}
}