using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.WatchOnlyWallet
{
	public class ClosedWatchOnlyWalletViewModel : ClosedWalletViewModel
	{
		internal ClosedWatchOnlyWalletViewModel(WalletManagerViewModel walletManager, Wallet wallet) : base(walletManager, wallet)
		{
		}
	}
}
