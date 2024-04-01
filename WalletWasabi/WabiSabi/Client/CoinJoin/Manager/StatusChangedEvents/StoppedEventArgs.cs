using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client.StatusChangedEvents;

public class StoppedEventArgs : StatusChangedEventArgs
{
	public StoppedEventArgs(IWallet wallet, StopReason reason)
		: base(wallet)
	{
		Reason = reason;
	}

	public StopReason Reason { get; }
}
