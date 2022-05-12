namespace WalletWasabi.Fluent.Helpers;

public static class ConfirmationTimeLabel
{
	public static string AxisLabel(TimeSpan timeSpan)
	{
		if (timeSpan <= TransactionFeeHelper.CalculateConfirmationTime(WalletWasabi.Helpers.Constants.FastestConfirmationTarget))
		{
			return "fastest";
		}

		return TimeSpanFormatter.Format(timeSpan, new TimeSpanFormatter.Configuration("d", "h", "m"));
	}

	public static string SliderLabel(TimeSpan timeSpan)
	{
		if (timeSpan <= TransactionFeeHelper.CalculateConfirmationTime(WalletWasabi.Helpers.Constants.FastestConfirmationTarget))
		{
			return "fastest";
		}

		return "~" + TimeSpanFormatter.Format(timeSpan, new TimeSpanFormatter.Configuration(" days", " hours", " minutes"));
	}
}