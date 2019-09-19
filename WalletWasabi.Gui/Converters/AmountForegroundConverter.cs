using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WalletWasabi.Exceptions;

namespace WalletWasabi.Gui.Converters
{
	public class AmountForegroundConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is string amount)
			{
				// When the amount starts with a '~' then Max is selected
				return amount.StartsWith("~")
					? Brushes.ForestGreen
					: amount.Equals("No Coins Selected", StringComparison.OrdinalIgnoreCase)
						? Brushes.IndianRed
						: Brushes.White;
			}
			else
			{
				throw new TypeArgumentException(value, typeof(string), nameof(value));
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
