using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace WalletWasabi.Gui.Converters
{
	public class BooleanOnOffConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is bool on)
			{
				return on ? "On" : "Off";
			}
			else
			{
				throw new TypeArgumentException(value, typeof(bool), nameof(value));
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
