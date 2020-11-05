using ReactiveUI;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets
{
	public class ClosedWatchOnlyWalletViewModel : ClosedWalletViewModel
	{
		internal ClosedWatchOnlyWalletViewModel(NavigationStateViewModel navigationState, WalletManager walletManager, Wallet wallet) : base(navigationState, walletManager, wallet)
		{
		}
	}
}
