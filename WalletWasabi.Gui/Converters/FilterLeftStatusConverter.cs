using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WalletWasabi.Gui.Converters
{
	public class FilterLeftStatusConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is int integer)
			{
				if (integer <= 0)
				{
					return "";
				}
				else if (integer == 1)
				{
					return "Downloading a filter, your wallet history may be incorrect...";
				}
				else
				{
					return $"Downloading {value} filters, your wallet history may be incorrect...";
				}
			}
			else
			{
				throw new TypeArgumentException(value, typeof(int), nameof(value));
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
