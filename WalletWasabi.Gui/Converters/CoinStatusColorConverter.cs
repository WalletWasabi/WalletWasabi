using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WalletWasabi.Exceptions;
using WalletWasabi.Gui.Controls.WalletExplorer;
using WalletWasabi.Gui.Models;

namespace WalletWasabi.Gui.Converters
{
	public class CoinStatusColorConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is SmartCoinStatus status)
			{
				return status switch
				{
					SmartCoinStatus.Confirmed => Brushes.Transparent,
					SmartCoinStatus.Unconfirmed => Brushes.Transparent,
					SmartCoinStatus.MixingOnWaitingList => Brushes.WhiteSmoke,
					SmartCoinStatus.MixingBanned => Brushes.IndianRed,
					SmartCoinStatus.MixingInputRegistration => Brushes.LimeGreen,
					SmartCoinStatus.MixingConnectionConfirmation => Brushes.DarkGreen,
					SmartCoinStatus.MixingOutputRegistration => Brushes.DarkGreen,
					SmartCoinStatus.MixingSigning => Brushes.DarkGreen,
					SmartCoinStatus.SpentAccordingToBackend => Brushes.IndianRed,
					SmartCoinStatus.MixingWaitingForConfirmation => Brushes.LightYellow,
					_ => throw new NotSupportedException() // Or rather not implemented?
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
