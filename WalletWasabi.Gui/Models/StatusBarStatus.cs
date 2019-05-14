using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Gui.Models
{
	public enum StatusBarStatus
	{
		Ready,
		CriticalUpdate,
		OptionalUpdate,
		Connecting,
		Synchronizing,
		Loading,
		SettingUpHardwareWallet,
		ConnectingToHardwareWallet,
		AcquiringXpubFromHardwareWallet,
		AcquiringSignatureFromHardwareWallet,
		BuildingTransaction,
		SigningTransaction,
		BroadcastingTransaction,
		DequeuingSelectedCoins
	}
}
