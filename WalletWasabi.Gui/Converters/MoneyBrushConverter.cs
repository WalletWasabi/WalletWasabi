using Avalonia.Data.Converters;
using Avalonia.Media;
using AvalonStudio.Extensibility.Theme;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WalletWasabi.Exceptions;
using WalletWasabi.Models;

namespace WalletWasabi.Gui.Converters
{
	public class MoneyBrushConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			decimal money;

			if (value is string moneyString)
			{
				money = decimal.Parse(moneyString);
			}
			else if (value is Money inMoney)
			{
				money = inMoney.ToDecimal(MoneyUnit.BTC);
			}
			else
			{
				return Brushes.IndianRed;
			}

			return money < 0 ? Brushes.IndianRed : Brushes.MediumSeaGreen;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
