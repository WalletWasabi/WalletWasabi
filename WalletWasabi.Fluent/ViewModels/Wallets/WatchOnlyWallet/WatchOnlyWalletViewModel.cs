using WalletWasabi.Gui;
using WalletWasabi.Services;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.WatchOnlyWallet
{
	public class WatchOnlyWalletViewModel : WalletViewModel
	{
		internal WatchOnlyWalletViewModel(UiConfig uiConfig, Wallet wallet, LegalChecker legalChecker) : base(uiConfig, wallet, legalChecker)
		{
		}
	}
}
