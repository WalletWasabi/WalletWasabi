using ReactiveUI;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets
{
	public class ClosedWatchOnlyWalletViewModel : ClosedWalletViewModel
	{
		internal ClosedWatchOnlyWalletViewModel(IScreen screen, WalletManager walletManager, Wallet wallet) : base(screen, walletManager, wallet)
		{
		}
	}
}
