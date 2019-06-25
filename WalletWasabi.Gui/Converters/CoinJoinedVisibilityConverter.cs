using Avalonia.Data.Converters;
using NBitcoin;
using System;
using System.Globalization;

namespace WalletWasabi.Gui.Converters
{
    public class CoinJoinedVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is Money money)
			{
				return money > Money.Zero;
			}

			throw new InvalidOperationException();
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
