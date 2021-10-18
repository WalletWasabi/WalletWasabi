using Avalonia.Data.Converters;
using System;
using System.Globalization;
using WalletWasabi.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Exceptions;

namespace WalletWasabi.Fluent.Converters
{
	public class FeeTargetTimeConverter : IValueConverter
	{
		public static string Convert(int feeTarget, string minutesLabel, string hourLabel, string hoursLabel, string dayLabel, string daysLabel)
		{
			if (feeTarget == Constants.FastestConfirmationTarget)
			{
				return "fastest";
			}
			else if (feeTarget is >= Constants.TwentyMinutesConfirmationTarget and <= 6) // minutes
			{
				return $"{feeTarget}0{minutesLabel}";
			}
			else if (feeTarget is >= 7 and <= Constants.OneDayConfirmationTarget) // hours
			{
				var hours = feeTarget / 6; // 6 blocks per hour
				return $"{hours}{IfPlural(hours, hourLabel, hoursLabel)}";
			}
			else if (feeTarget is >= (Constants.OneDayConfirmationTarget + 1) and < Constants.SevenDaysConfirmationTarget) // days
			{
				var days = feeTarget / Constants.OneDayConfirmationTarget;
				return $"{days}{IfPlural(days, dayLabel, daysLabel)}";
			}
			else if (feeTarget == Constants.SevenDaysConfirmationTarget)
			{
				return "one week";
			}
			else
			{
				return "invalid";
			}
		}

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is int feeTarget)
			{
				return FeeTargetTimeConverter.Convert(feeTarget, " minutes", " hour", " hours", " day", " days");
			}
			else
			{
				throw new TypeArgumentException(value, typeof(SmartCoinStatus), nameof(value));
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}

		private static string IfPlural(int val, string singular, string plural)
		{
			return val == 1 ? singular : plural;
		}
	}
}
