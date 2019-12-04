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
			if (value is StatusBarStatus status)
			{
				return status switch
				{
					StatusBarStatus.Ready => "Ready",
					StatusBarStatus.CriticalUpdate => "THE BACKEND WAS UPGRADED WITH BREAKING CHANGES - PLEASE UPDATE YOUR WASABI WALLET!",
					StatusBarStatus.OptionalUpdate => "A new version of Wasabi Wallet is available.",
					StatusBarStatus.Connecting => "Connecting...",
					StatusBarStatus.Synchronizing => "Synchronizing...",
					StatusBarStatus.Loading => "Loading...",
					StatusBarStatus.SettingUpHardwareWallet => "Setting up hardware wallet...",
					StatusBarStatus.ConnectingToHardwareWallet => "Connecting to hardware wallet...",
					StatusBarStatus.AcquiringXpubFromHardwareWallet => "Acquiring xpub from hardware wallet...",
					StatusBarStatus.AcquiringSignatureFromHardwareWallet => "Acquiring signature from hardware wallet...",
					StatusBarStatus.BuildingTransaction => "Building transaction...",
					StatusBarStatus.SigningTransaction => "Signing transaction...",
					StatusBarStatus.BroadcastingTransaction => "Broadcasting transaction...",
					StatusBarStatus.DequeuingSelectedCoins => "Dequeuing selected coins...",
					_ => status.ToString()
				};
			}
			else
			{
				throw new TypeArgumentException(value, typeof(StatusBarStatus), nameof(value));
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
