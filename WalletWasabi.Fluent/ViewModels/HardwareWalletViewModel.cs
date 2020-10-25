using ReactiveUI;
using WalletWasabi.Gui;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels
{
	public class HardwareWalletViewModel : WalletViewModel
	{
		internal HardwareWalletViewModel(NavigationStateViewModel navigationState, UiConfig uiConfig, Wallet wallet) : base(navigationState, uiConfig, wallet)
		{
		}
	}
}
