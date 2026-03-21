using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client.StatusChangedEvents;

public enum StopReason
{
	WalletUnloaded
}

public enum CompletionStatus
{
	Success,
	Canceled,
	Failed,
	Unknown
}

public enum CoinjoinError
{
	NoCoinsEligibleToMix,
	AutoConjoinDisabled,
	UserInSendWorkflow,
	NotEnoughUnprivateBalance,
	AllCoinsPrivate,
	UserWasntInRound,
	NoConfirmedCoinsEligibleToMix,
	CoinsRejected,
	OnlyImmatureCoinsAvailable,
	OnlyExcludedCoinsAvailable,
	MiningFeeRateTooHigh,
	MinInputCountTooLow,
	CoordinatorLiedAboutInputs,
	NotEnoughConfirmedUnprivateBalance
}

public class StatusChangedEventArgs : EventArgs
{
	public StatusChangedEventArgs(IWallet wallet)
	{
		Wallet = wallet;
	}

	public IWallet Wallet { get; }
}
