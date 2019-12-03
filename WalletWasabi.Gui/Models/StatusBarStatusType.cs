using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Gui.Models
{
	/// <summary>
	/// Order matter: piority the lower.
	/// </summary>
	public enum StatusBarStatusType
	{
		CriticalUpdate,
		OptionalUpdate,
		Connecting,
		Synchronizing,
		Loading,
		WalletServiceLoadingStarting,
		WalletServiceLoadingWaitingForBitcoinStore,
		WalletServiceLoadingProcessingTransactions,
		WalletServiceLoadingProcessingFilters,
		WalletServiceLoadingProcessingMempool,
		WalletServiceLoadingCompleted,
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
