using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Gui.Models.StatusBarStatuses
{
	/// <summary>
	/// Order: piority the lower.
	/// </summary>
	public enum StatusPriority
	{
		CriticalUpdate,
		OptionalUpdate,
		Connecting,
		Synchronizing,
		Loading,
		BroadcastingTransaction,
		SigningTransaction,
		AcquiringSignatureFromHardwareWallet,
		AcquiringXpubFromHardwareWallet,
		ConnectingToHardwareWallet,
		SettingUpHardwareWallet,
		BuildingTransaction,
		DequeuingSelectedCoins,
		Ready
	}
}
