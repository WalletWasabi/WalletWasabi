using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WalletWasabi.Gui.Models;

namespace WalletWasabi.Gui.Converters
{
	public class SelectNonPrivateCheckBoxVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is CoinListContainerType.CoinJoinTabViewModel)
			{
				return true;
			}
			else
			{
				return false;
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
