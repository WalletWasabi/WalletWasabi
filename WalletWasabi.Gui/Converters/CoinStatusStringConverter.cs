using Avalonia.Data.Converters;
using System;
using System.Globalization;
using WalletWasabi.Exceptions;
using WalletWasabi.Gui.Models;

namespace WalletWasabi.Gui.Converters
{
	public class CoinStatusStringConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is SmartCoinStatus status)
			{
				return status switch
				{
					SmartCoinStatus.Confirmed => "",
					SmartCoinStatus.Unconfirmed => "",
					SmartCoinStatus.MixingOnWaitingList => " queued  ",
					SmartCoinStatus.MixingBanned => " banned  ",
					SmartCoinStatus.MixingInputRegistration => " registered  ",
					SmartCoinStatus.MixingConnectionConfirmation => " connection confirmed  ",
					SmartCoinStatus.MixingOutputRegistration => " output registered  ",
					SmartCoinStatus.MixingSigning => " signed  ",
					SmartCoinStatus.SpentAccordingToBackend => " spent  ",
					SmartCoinStatus.MixingWaitingForConfirmation => " waiting for confirmation  ",
					_ => ""
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
