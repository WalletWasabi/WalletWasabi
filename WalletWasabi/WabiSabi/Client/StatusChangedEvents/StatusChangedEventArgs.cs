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
	NoCoinsToMix,
	AutoConjoinDisabled,
	UserInSendWorkflow,
	NotEnoughUnprivateBalance,
	BackendNotSynchronized,
	AllCoinsPrivate,
	UserWasntInRound
}

public class StatusChangedEventArgs : EventArgs
{
	public StatusChangedEventArgs(IWallet wallet)
	{
		Wallet = wallet;
	}

	public IWallet Wallet { get; }
}
