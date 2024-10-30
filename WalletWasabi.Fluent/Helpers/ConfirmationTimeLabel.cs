namespace WalletWasabi.Fluent.Helpers;

public static class ConfirmationTimeLabel
{
	public static string AxisLabel(TimeSpan timeSpan)
	{
		if (timeSpan <= TransactionFeeHelper.CalculateConfirmationTime(WalletWasabi.Helpers.Constants.FastestConfirmationTarget))
		{
			return Lang.Utils.LowerCaseFirst("Words_Fastest");
		}

		return TimeSpanFormatter.Format(timeSpan, new TimeSpanFormatter.Configuration(false));
	}

	public static string SliderLabel(TimeSpan timeSpan)
	{
		if (timeSpan <= TransactionFeeHelper.CalculateConfirmationTime(WalletWasabi.Helpers.Constants.FastestConfirmationTarget))
		{
			return Lang.Utils.LowerCaseFirst("Words_Fastest");
		}

		return "~" + TimeSpanFormatter.Format(timeSpan, new TimeSpanFormatter.Configuration(true));
	}
}
