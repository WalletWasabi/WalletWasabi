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
			if (value is string moneyString)
			{
				var money = decimal.Parse(moneyString.Replace(" ", string.Empty));
				return money < 0 ? Brushes.IndianRed : Brushes.MediumSeaGreen;
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
