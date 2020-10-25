using ReactiveUI;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels
{
	public class ClosedHardwareWalletViewModel : ClosedWalletViewModel
	{
		internal ClosedHardwareWalletViewModel(NavigationStateViewModel navigationState, WalletManager walletManager, Wallet wallet) : base(navigationState, walletManager, wallet)
		{
		}
	}
}
