using ReactiveUI;
using WalletWasabi.Gui;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets
{
	public class WatchOnlyWalletViewModel : WalletViewModel
	{
		internal WatchOnlyWalletViewModel(NavigationStateViewModel navigationState, UiConfig uiConfig, Wallet wallet) : base(navigationState, uiConfig, wallet)
		{
		}
	}
}
