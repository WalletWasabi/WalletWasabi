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
				string friendlyStatus = status.Type switch
				{
					StatusBarStatusType.Ready => "Ready",
					StatusBarStatusType.CriticalUpdate => "THE BACKEND WAS UPGRADED WITH BREAKING CHANGES - PLEASE UPDATE YOUR WASABI WALLET!",
					StatusBarStatusType.OptionalUpdate => "A new version of Wasabi Wallet is available.",
					StatusBarStatusType.Connecting => "Connecting...",
					StatusBarStatusType.Synchronizing => "Synchronizing...",
					StatusBarStatusType.Loading => "Loading...",
					StatusBarStatusType.WalletServiceLoadingStarting => "Loading wallet...",
					StatusBarStatusType.WalletServiceLoadingWaitingForBitcoinStore => "Waiting for external services...",
					StatusBarStatusType.WalletServiceLoadingProcessingTransactions => "Processing transactions...",
					StatusBarStatusType.WalletServiceLoadingProcessingFilters => "Reindexing filters...",
					StatusBarStatusType.WalletServiceLoadingProcessingMempool => "Processing mempool...",
					StatusBarStatusType.WalletServiceLoadingCompleted => "Wallet loading completed.",
					StatusBarStatusType.SettingUpHardwareWallet => "Setting up hardware wallet...",
					StatusBarStatusType.ConnectingToHardwareWallet => "Connecting to hardware wallet...",
					StatusBarStatusType.AcquiringXpubFromHardwareWallet => "Acquiring xpub from hardware wallet...",
					StatusBarStatusType.AcquiringSignatureFromHardwareWallet => "Acquiring signature from hardware wallet...",
					StatusBarStatusType.BuildingTransaction => "Building transaction...",
					StatusBarStatusType.SigningTransaction => "Signing transaction...",
					StatusBarStatusType.BroadcastingTransaction => "Broadcasting transaction...",
					StatusBarStatusType.DequeuingSelectedCoins => "Dequeuing selected coins...",
					_ => status.ToString()
				};

				if (status.ProgressPercentage < 0)
				{
					return friendlyStatus;
				}
				else
				{
					return $"{friendlyStatus} {(ulong)status.ProgressPercentage}%";
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
