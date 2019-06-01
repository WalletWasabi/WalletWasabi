using Avalonia.Data.Converters;
using Avalonia.Media;
using AvalonStudio.Extensibility.Theme;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WalletWasabi.Models;

namespace WalletWasabi.Gui.Converters
{
	public class MoneyBrushConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var money = decimal.Parse((string)value);
			if (money < 0)
			{
				return Brushes.IndianRed;
			}
			else
			{
				return Brushes.MediumSeaGreen;
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
