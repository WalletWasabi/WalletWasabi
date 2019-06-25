using Avalonia.Data.Converters;
using NBitcoin;
using System;
using System.Globalization;

namespace WalletWasabi.Gui.Converters
{
	public class MoneyStringConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is Money money)
			{
				return money.ToString(fplus: false, trimExcessZero: true);
			}
			else if (value is null)
			{
				return "0";
			}

			throw new InvalidOperationException();
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
