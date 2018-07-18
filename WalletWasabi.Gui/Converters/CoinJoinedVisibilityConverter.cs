using Avalonia.Data.Converters;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WalletWasabi.Gui.Converters
{
	public class CoinJoinedVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is Money money)
			{
				if (money <= Money.Zero)
				{
					return false;
				}
				else
				{
					return true;
				}
			}

			throw new InvalidOperationException();
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
