using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client.StatusChangedEvents;

public class WalletStoppedCoinJoinEventArgs : StatusChangedEventArgs
{
	public WalletStoppedCoinJoinEventArgs(Wallet wallet) : base(wallet)
	{
	}
}
