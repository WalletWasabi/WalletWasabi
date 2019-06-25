using Avalonia.Data.Converters;
using Avalonia.Input;
using System;
using System.Globalization;

namespace WalletWasabi.Gui.Converters
{
    public class ShowCursorConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is bool boolean && boolean)
			{
				return new Cursor(StandardCursorType.Hand);
			}

			return new Cursor(StandardCursorType.Arrow);
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
