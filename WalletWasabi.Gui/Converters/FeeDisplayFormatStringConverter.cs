using Avalonia.Data.Converters;
using System;
using System.Globalization;
using WalletWasabi.Gui.Models;
using WalletWasabi.Exceptions;
using System.Collections.Generic;

namespace WalletWasabi.Gui.Converters
{
	public class FeeDisplayFormatStringConverter : IValueConverter
	{
		private Dictionary<FeeDisplayFormat, string> Texts { get; } = new Dictionary<FeeDisplayFormat, string>
		{
			{ FeeDisplayFormat.SatoshiPerByte, "Satoshi / vbyte" },
			{ FeeDisplayFormat.USD, "USD" },
			{ FeeDisplayFormat.BTC, "BTC" },
			{ FeeDisplayFormat.Percentage, "Percentage" },
		};

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is FeeDisplayFormat format)
			{
				return Texts[format];
			}

			throw new TypeArgumentException(value, typeof(FeeDisplayFormat), nameof(value));
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
