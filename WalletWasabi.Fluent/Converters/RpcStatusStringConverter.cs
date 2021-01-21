using Avalonia.Data.Converters;
using System;
using System.Globalization;
using WalletWasabi.BitcoinCore.Monitoring;
using WalletWasabi.Exceptions;

namespace WalletWasabi.Fluent.Converters
{
	public class RpcStatusStringConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is null)
			{
				return "";
			}
			if (value is RpcStatus val)
			{
				return val.ToString();
			}
			else
			{
				throw new TypeArgumentException(value, typeof(RpcStatus), nameof(value));
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
