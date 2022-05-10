using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client.StatusChangedEvents;

public class CompletedEventArgs : StatusChangedEventArgs
{
	public CompletedEventArgs(Wallet wallet, CompletionStatus completionStatus)
		: base(wallet)
	{
		CompletionStatus = completionStatus;
	}

	public CompletionStatus CompletionStatus { get; }
}
