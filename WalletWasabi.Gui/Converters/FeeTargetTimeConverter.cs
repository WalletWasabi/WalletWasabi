using Avalonia.Data.Converters;
using System;
using System.Globalization;
using WalletWasabi.Helpers;
using WalletWasabi.Gui.Models;
using WalletWasabi.Exceptions;

namespace WalletWasabi.Gui.Converters
{
	public class FeeTargetTimeConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is int feeTarget)
			{
				if (feeTarget is >= Constants.TwentyMinutesConfirmationTarget and <= 6) // minutes
				{
					return $"{feeTarget}0 minutes";
				}
				else if (feeTarget is >= 7 and <= Constants.OneDayConfirmationTarget) // hours
				{
					var hours = feeTarget / 6; // 6 blocks per hour
					return $"{hours} {IfPlural(hours, "hour", "hours")}";
				}
				else if (feeTarget is >= (Constants.OneDayConfirmationTarget + 1) and < Constants.SevenDaysConfirmationTarget) // days
				{
					var days = feeTarget / Constants.OneDayConfirmationTarget;
					return $"{days} {IfPlural(days, "day", "days")}";
				}
				else if (feeTarget == Constants.SevenDaysConfirmationTarget)
				{
					return "one week";
				}
				else
				{
					return "Invalid";
				}
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
