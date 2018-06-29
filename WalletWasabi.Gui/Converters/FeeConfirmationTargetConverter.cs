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
					return $"Minutes";
				}
				if (integer >= 7 && integer <= 144) // hours
				{
					return "Hours";
				}
				if (integer >= 145 && integer <= 1008) // days
				{
					return "Days";
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
