using WalletWasabi.Gui;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets
{
	public class WatchOnlyWalletViewModel : WalletViewModel
	{
		internal WatchOnlyWalletViewModel(UiConfig uiConfig, Wallet wallet) : base(uiConfig, wallet)
		{
		}
	}
}
