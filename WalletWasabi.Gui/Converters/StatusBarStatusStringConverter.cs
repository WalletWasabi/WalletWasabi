using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WalletWasabi.Exceptions;
using WalletWasabi.Gui.Models.StatusBarStatuses;

namespace WalletWasabi.Gui.Converters
{
	public class StatusBarStatusStringConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is StatusPriority status)
			{
				return status switch
				{
					StatusPriority.Ready => "Ready",
					StatusPriority.CriticalUpdate => "THE BACKEND WAS UPGRADED WITH BREAKING CHANGES - PLEASE UPDATE YOUR WASABI WALLET!",
					StatusPriority.OptionalUpdate => "A new version of Wasabi Wallet is available.",
					StatusPriority.Connecting => "Connecting...",
					StatusPriority.Synchronizing => "Synchronizing...",
					StatusPriority.Loading => "Loading...",
					StatusPriority.SettingUpHardwareWallet => "Setting up hardware wallet...",
					StatusPriority.ConnectingToHardwareWallet => "Connecting to hardware wallet...",
					StatusPriority.AcquiringXpubFromHardwareWallet => "Acquiring xpub from hardware wallet...",
					StatusPriority.AcquiringSignatureFromHardwareWallet => "Acquiring signature from hardware wallet...",
					StatusPriority.BuildingTransaction => "Building transaction...",
					StatusPriority.SigningTransaction => "Signing transaction...",
					StatusPriority.BroadcastingTransaction => "Broadcasting transaction...",
					StatusPriority.DequeuingSelectedCoins => "Dequeuing selected coins...",
					_ => status.ToString()
				};
			}
			else
			{
				throw new TypeArgumentException(value, typeof(StatusPriority), nameof(value));
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
