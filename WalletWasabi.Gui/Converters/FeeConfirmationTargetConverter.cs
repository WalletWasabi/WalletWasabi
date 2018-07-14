using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace WalletWasabi.Gui.Converters
{
	public class FeeConfirmationTargetConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is int integer)
			{
				if (integer >= 2 && integer <= 6) // minutes
				{
					return $"{integer}0 minutes";
				}
				if (integer >= 7 && integer <= 144) // hours
				{
					var h = integer / 6;
					return $"{h} hours";
				}
				if (integer >= 145 && integer <= 1008) // days
				{
					var d = integer / 144;
					return $"{d} days";
				}
			}

			throw new InvalidOperationException();
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
