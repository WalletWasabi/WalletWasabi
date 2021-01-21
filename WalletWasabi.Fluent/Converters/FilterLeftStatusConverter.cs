using Avalonia.Data.Converters;
using System;
using System.Globalization;
using WalletWasabi.Exceptions;

namespace WalletWasabi.Fluent.Converters
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
					return "Downloading a filter...";
				}
				else
				{
					return $"Downloading {value} filters...";
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
