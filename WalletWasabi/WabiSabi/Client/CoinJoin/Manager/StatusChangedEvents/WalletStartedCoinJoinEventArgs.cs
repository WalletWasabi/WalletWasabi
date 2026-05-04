using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client.StatusChangedEvents;

public class WalletStartedCoinJoinEventArgs : StatusChangedEventArgs
{
	public WalletStartedCoinJoinEventArgs(Wallet wallet) : base(wallet)
	{
	}
}
