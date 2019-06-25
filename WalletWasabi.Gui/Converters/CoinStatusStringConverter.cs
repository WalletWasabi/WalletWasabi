using Avalonia.Data.Converters;
using System;
using System.Globalization;
using WalletWasabi.Gui.Models;

namespace WalletWasabi.Gui.Converters
{
	public class CoinStatusStringConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is SmartCoinStatus status)
			{
				switch (status)
				{
					case SmartCoinStatus.Confirmed: return "";
					case SmartCoinStatus.Unconfirmed: return "";
					case SmartCoinStatus.MixingOnWaitingList: return " queued  ";
					case SmartCoinStatus.MixingBanned: return " banned  ";
					case SmartCoinStatus.MixingInputRegistration: return " registered  ";
					case SmartCoinStatus.MixingConnectionConfirmation: return " connection confirmed  ";
					case SmartCoinStatus.MixingOutputRegistration: return " output registered  ";
					case SmartCoinStatus.MixingSigning: return " signed  ";
					case SmartCoinStatus.SpentAccordingToBackend: return " spent  ";
					case SmartCoinStatus.MixingWaitingForConfirmation: return " waiting for confirmation  ";
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
