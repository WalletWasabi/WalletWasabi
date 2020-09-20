namespace WalletWasabi.Gui.Models.StatusBarStatuses
{
	/// <summary>
	/// Order: priority the lower.
	/// </summary>
	public enum StatusType
	{
		CriticalUpdate,
		OptionalUpdate,
		Connecting,
		WalletProcessingFilters,
		WalletProcessingTransactions,
		WalletLoading,
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
