using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;
using WalletWasabi.Gui.Models;

namespace WalletWasabi.Gui.Converters
{
    public class CoinStatusForegroundConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is SmartCoinStatus status)
			{
				switch (status)
				{
					case SmartCoinStatus.MixingInputRegistration:
					case SmartCoinStatus.MixingOnWaitingList:
					case SmartCoinStatus.MixingWaitingForConfirmation: return Brushes.Black;
					default: return Brushes.White;
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
