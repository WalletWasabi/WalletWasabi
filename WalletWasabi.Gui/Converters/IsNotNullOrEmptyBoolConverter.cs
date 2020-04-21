using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace WalletWasabi.Gui.Converters
{
	internal class IsNotNullOrEmptyBoolConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is null)
			{
				return false;
			}

			if (!string.IsNullOrEmpty(value.ToString()))
			{
				return true;
			}

			return false;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
