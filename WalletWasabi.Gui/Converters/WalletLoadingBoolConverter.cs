using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WalletWasabi.Exceptions;
using WalletWasabi.Wallets;

namespace WalletWasabi.Gui.Converters
{
	public class WalletLoadingBoolConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is WalletState state)
			{
				return state < WalletState.Started && state > WalletState.Uninitialized;
			}
			else
			{
				throw new TypeArgumentException(value, typeof(WalletState), nameof(value));
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
