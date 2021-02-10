using WalletWasabi.Gui;
using WalletWasabi.Services;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.HardwareWallet
{
	public class HardwareWalletViewModel : WalletViewModel
	{
		internal HardwareWalletViewModel(UiConfig uiConfig, Wallet wallet, LegalChecker legalChecker) : base(uiConfig, wallet, legalChecker)
		{
		}
	}
}
