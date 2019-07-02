using Avalonia.Data.Converters;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

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
			else
			{
				throw new TypeArgumentException(value, typeof(Money), nameof(value));
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
