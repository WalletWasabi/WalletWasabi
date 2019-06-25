using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace WalletWasabi.Gui.Converters
{
    public class MoneyBrushConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var money = decimal.Parse((string)value);
			if (money < 0)
			{
				return Brushes.IndianRed;
			}
			else
			{
				return Brushes.MediumSeaGreen;
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
