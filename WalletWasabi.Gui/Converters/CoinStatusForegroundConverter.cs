using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;
using WalletWasabi.Exceptions;
using WalletWasabi.Gui.Models;

namespace WalletWasabi.Gui.Converters
{
	public class CoinStatusForegroundConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is SmartCoinStatus status)
			{
				return status switch
				{
					SmartCoinStatus.MixingInputRegistration or SmartCoinStatus.MixingOnWaitingList or SmartCoinStatus.MixingWaitingForConfirmation => Brushes.Black,
					_ => Brushes.White,
				};
			}
			else
			{
				throw new TypeArgumentException(value, typeof(SmartCoinStatus), nameof(value));
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
