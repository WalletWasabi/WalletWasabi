using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client.Events;

public class StoppedEventArgs : StatusChangedEventArgs
{
	public StoppedEventArgs(Wallet wallet, StopReason reason)
		: base(wallet)
	{
		Reason = reason;
	}

	public StopReason Reason { get; }
}
