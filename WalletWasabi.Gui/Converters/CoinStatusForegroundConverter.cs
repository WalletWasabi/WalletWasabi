using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WalletWasabi.Gui.Controls.WalletExplorer;
using WalletWasabi.Gui.Models;
using WalletWasabi.Models.ChaumianCoinJoin;

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
