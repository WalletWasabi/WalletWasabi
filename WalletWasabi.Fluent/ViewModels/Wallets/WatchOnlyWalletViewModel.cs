using ReactiveUI;
using WalletWasabi.Gui;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets
{
	public class WatchOnlyWalletViewModel : WalletViewModel
	{
		internal WatchOnlyWalletViewModel(IScreen screen, UiConfig uiConfig, Wallet wallet) : base(screen, uiConfig, wallet)
		{
		}
	}
}
