using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets;
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
