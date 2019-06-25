using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;
using WalletWasabi.Gui.Models;

namespace WalletWasabi.Gui.Converters
{
    public class CoinStatusBorderBrushConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is SmartCoinStatus status)
			{
				switch (status)
				{
					case SmartCoinStatus.Confirmed: return Brushes.Transparent;
					case SmartCoinStatus.Unconfirmed: return Brushes.Transparent;
					default: return Brushes.Black;
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
