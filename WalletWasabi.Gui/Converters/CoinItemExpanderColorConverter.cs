using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WalletWasabi.Exceptions;

namespace WalletWasabi.Gui.Converters
{
	public class CoinItemExpanderColorConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is bool expanded)
			{
				return expanded ? Application.Current.Resources[Global.ThemeBackgroundBrushResourceKey] as IBrush : Brushes.Transparent;
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
