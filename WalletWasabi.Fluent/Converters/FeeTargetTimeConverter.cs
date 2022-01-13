using Avalonia.Data.Converters;
using System.Globalization;
using Avalonia;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.Converters;

public class FeeTargetTimeConverter : IValueConverter
{
	public static string Convert(int feeTarget, string minutesLabel, string hourLabel, string hoursLabel, string dayLabel, string daysLabel)
	{
		if (feeTarget == Constants.FastestConfirmationTarget)
		{
			return "fastest";
		}

		if (feeTarget is >= Constants.TwentyMinutesConfirmationTarget and <= 6) // minutes
		{
			return $"{feeTarget}0{minutesLabel}";
		}

		if (feeTarget is >= 7 and <= Constants.OneDayConfirmationTarget) // hours
		{
			var hours = feeTarget / 6; // 6 blocks per hour
			return $"{hours}{IfPlural(hours, hourLabel, hoursLabel)}";
		}

		if (feeTarget is >= (Constants.OneDayConfirmationTarget + 1) and < Constants.SevenDaysConfirmationTarget) // days
		{
			var days = feeTarget / Constants.OneDayConfirmationTarget;
			return $"{days}{IfPlural(days, dayLabel, daysLabel)}";
		}

		if (feeTarget == Constants.SevenDaysConfirmationTarget)
		{
			return "one week";
		}

		return "> one week";
	}

	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is int feeTarget)
		{
			return Convert(feeTarget, " minutes", " hour", " hours", " day", " days");
		}

		return AvaloniaProperty.UnsetValue;
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}

	private static string IfPlural(int val, string singular, string plural)
	{
		return val == 1 ? singular : plural;
	}
}
