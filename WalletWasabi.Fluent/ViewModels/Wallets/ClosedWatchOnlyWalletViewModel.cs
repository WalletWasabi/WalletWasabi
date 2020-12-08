using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets
{
	public class ClosedWatchOnlyWalletViewModel : ClosedWalletViewModel
	{
		internal ClosedWatchOnlyWalletViewModel(WalletManager walletManager, Wallet wallet) : base(walletManager, wallet)
		{
		}
	}
}
