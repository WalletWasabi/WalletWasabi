using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.Gui.Models.StatusBarStatuses
{
	public class Status
	{
		public static Status Started(StatusType type) => new Status(type, 0);

		public static Status Completed(StatusType type) => new Status(type, 100);

		public Status(StatusType type, ushort percentage)
		{
			Type = type;
			Percentage = Guard.InRangeAndNotNull(nameof(percentage), percentage, 0, 100);
		}

		public StatusType Type { get; }
		public int Percentage { get; }

		public override string ToString()
		{
			var friendlyName = Type switch
			{
				StatusType.Ready => "Ready",
				StatusType.CriticalUpdate => "THE BACKEND WAS UPGRADED WITH BREAKING CHANGES - PLEASE UPDATE YOUR WASABI WALLET!",
				StatusType.OptionalUpdate => "A new version of Wasabi Wallet is available.",
				StatusType.Connecting => "Connecting...",
				StatusType.Synchronizing => "Synchronizing...",
				StatusType.Loading => "Loading...",
				StatusType.SettingUpHardwareWallet => "Setting up hardware wallet...",
				StatusType.ConnectingToHardwareWallet => "Connecting to hardware wallet...",
				StatusType.AcquiringXpubFromHardwareWallet => "Acquiring xpub from hardware wallet...",
				StatusType.AcquiringSignatureFromHardwareWallet => "Acquiring signature from hardware wallet...",
				StatusType.BuildingTransaction => "Building transaction...",
				StatusType.SigningTransaction => "Signing transaction...",
				StatusType.BroadcastingTransaction => "Broadcasting transaction...",
				StatusType.DequeuingSelectedCoins => "Dequeuing selected coins...",
				_ => Type.ToString()
			};

			if (Percentage == 0)
			{
				return friendlyName;
			}
			else
			{
				return $"{friendlyName} {Percentage}%";
			}
		}
	}
}
