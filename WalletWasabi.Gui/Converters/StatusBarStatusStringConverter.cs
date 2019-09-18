using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WalletWasabi.Exceptions;
using WalletWasabi.Gui.Models;

namespace WalletWasabi.Gui.Converters
{
	public class StatusBarStatusStringConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is StatusBarStatus status)
			{
				switch (status)
				{
					case StatusBarStatus.Ready: return "Ready";
					case StatusBarStatus.CriticalUpdate: return "THE BACKEND WAS UPGRADED WITH BREAKING CHANGES - PLEASE UPDATE YOUR WASABI WALLET!";
					case StatusBarStatus.OptionalUpdate: return "A new version of Wasabi Wallet is available.";
					case StatusBarStatus.Connecting: return "Connecting...";
					case StatusBarStatus.Synchronizing: return "Synchronizing...";
					case StatusBarStatus.Loading: return "Loading...";
					case StatusBarStatus.SettingUpHardwareWallet: return "Setting up hardware wallet...";
					case StatusBarStatus.ConnectingToHardwareWallet: return "Connecting to hardware wallet...";
					case StatusBarStatus.AcquiringXpubFromHardwareWallet: return "Acquiring xpub from hardware wallet...";
					case StatusBarStatus.AcquiringSignatureFromHardwareWallet: return "Acquiring signature from hardware wallet...";
					case StatusBarStatus.BuildingTransaction: return "Building transaction...";
					case StatusBarStatus.SigningTransaction: return "Signing transaction...";
					case StatusBarStatus.BroadcastingTransaction: return "Broadcasting transaction...";
					case StatusBarStatus.DequeuingSelectedCoins: return "Dequeuing selected coins...";
					default: return status.ToString();
				}
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
