using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace WalletWasabi.Gui.Converters
{
	public class ShowHideSensitiveKeysBoolConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is bool isShow)
			{
				return isShow ? "Hide Sensitive Keys" : "Show Sensitive Keys";
			}
			else
			{
				throw new TypeArgumentException(value, typeof(bool), nameof(value));
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
