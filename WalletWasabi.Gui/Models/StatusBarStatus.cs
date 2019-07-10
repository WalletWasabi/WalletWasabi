using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Gui.Models
{
	/// <summary>
	/// Order matter: piority the lower.
	/// </summary>
	public enum StatusBarStatus
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
		DequeueingSelectedCoins,
		Ready
	}
}
