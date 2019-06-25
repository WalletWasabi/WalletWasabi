using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace WalletWasabi.Gui.Converters
{
    public class AmountForegroundConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is bool boolean)
			{
				if (boolean)
				{
					return Brushes.ForestGreen;
				}
				else
				{
					return Brushes.White;
				}
			}

			throw new InvalidOperationException();
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
