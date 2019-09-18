using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WalletWasabi.Exceptions;
using WalletWasabi.Gui.Controls.WalletExplorer;
using WalletWasabi.Gui.Models;
using WalletWasabi.Models.ChaumianCoinJoin;

namespace WalletWasabi.Gui.Converters
{
	public class CoinStatusColorConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is SmartCoinStatus status)
			{
				switch (status)
				{
					case SmartCoinStatus.Confirmed: return Brushes.Transparent;
					case SmartCoinStatus.Unconfirmed: return Brushes.Transparent;
					case SmartCoinStatus.MixingOnWaitingList: return Brushes.WhiteSmoke;
					case SmartCoinStatus.MixingBanned: return Brushes.IndianRed;
					case SmartCoinStatus.MixingInputRegistration: return Brushes.LimeGreen;
					case SmartCoinStatus.MixingConnectionConfirmation: return Brushes.DarkGreen;
					case SmartCoinStatus.MixingOutputRegistration: return Brushes.DarkGreen;
					case SmartCoinStatus.MixingSigning: return Brushes.DarkGreen;
					case SmartCoinStatus.SpentAccordingToBackend: return Brushes.IndianRed;
					case SmartCoinStatus.MixingWaitingForConfirmation: return Brushes.LightYellow;
					default: throw new NotSupportedException(); // Or rather not implemented?
				}
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
