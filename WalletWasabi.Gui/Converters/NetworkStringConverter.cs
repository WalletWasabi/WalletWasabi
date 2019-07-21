using Avalonia.Data.Converters;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WalletWasabi.Gui.Converters
{
	public class NetworkStringConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is Network network)
			{
				return network.ToString();
			}
			else
			{
				throw new TypeArgumentException(value, typeof(Network), nameof(value));
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var networkString = value as string;

			return Network.GetNetwork(networkString);
		}
	}
}
