using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WalletWasabi.BitcoinCore.Monitor;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;

namespace WalletWasabi.Gui.Converters
{
	public class RpcStatusStringConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is null)
			{
				return string.Empty;
			}
			if (value is RpcStatus val)
			{
				return val.ToString();
			}
			else
			{
				throw new TypeArgumentException(value, typeof(Version), nameof(value));
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
