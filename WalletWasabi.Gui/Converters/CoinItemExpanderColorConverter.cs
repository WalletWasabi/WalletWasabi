using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace WalletWasabi.Gui.Converters
{
	public class CoinItemExpanderColorConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is bool expanded)
			{
				if (expanded)
				{
					return Application.Current.Resources[Global.ThemeBackgroundBrushResourceKey] as IBrush;
				}

				return Brushes.Transparent;
			}

			throw new InvalidOperationException();
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
