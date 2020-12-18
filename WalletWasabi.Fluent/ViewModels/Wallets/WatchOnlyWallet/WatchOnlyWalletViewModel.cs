using WalletWasabi.Gui;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.WatchOnlyWallet
{
	public class WatchOnlyWalletViewModel : WalletViewModel
	{
		internal WatchOnlyWalletViewModel(UiConfig uiConfig, Wallet wallet, WalletManager walletManager) : base(uiConfig, wallet, walletManager)
		{
		}
	}
}
