using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WalletWasabi.Exceptions;

namespace WalletWasabi.Gui.Converters
{
	public class UsdExchangeRateAmountToolTipConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is decimal usdExchangeRate)
			{
				// When the amount starts with a '~' then Max is selected
				return $"Exchange Rate: {(long)usdExchangeRate} BTC/USD.";
			}
			else
			{
				throw new TypeArgumentException(value, typeof(decimal), nameof(value));
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
