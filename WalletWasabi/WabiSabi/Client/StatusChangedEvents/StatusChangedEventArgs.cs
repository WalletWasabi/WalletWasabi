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
}

public class StatusChangedEventArgs : EventArgs
{
	public StatusChangedEventArgs(Wallet wallet)
	{
		Wallet = wallet;
	}

	public Wallet Wallet { get; }
}
