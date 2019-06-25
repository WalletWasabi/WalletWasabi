using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace WalletWasabi.Gui.Converters
{
    public class BooleanOnOffConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is bool boolean)
			{
				return (boolean) ? "On" : "Off";
			}

			throw new InvalidOperationException();
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
