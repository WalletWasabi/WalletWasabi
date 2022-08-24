using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client.StatusChangedEvents;

public class WalletStoppedCoinJoinEventArgs : StatusChangedEventArgs
{
	public WalletStoppedCoinJoinEventArgs(IWallet wallet) : base(wallet)
	{
	}
}
