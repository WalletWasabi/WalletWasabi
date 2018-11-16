using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WalletWasabi.Gui.Controls.WalletExplorer;
using WalletWasabi.Models.ChaumianCoinJoin;

namespace WalletWasabi.Gui.Converters
{
	public class CoinStatusColorConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is CoinStatusEnum status)
			{
				switch (status)
				{
					case CoinStatusEnum.None:
						return Brushes.Black;
					default:
						return Brushes.Transparent;
				}
			}
			throw new InvalidOperationException();
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new InvalidOperationException();
		}
	}
}
