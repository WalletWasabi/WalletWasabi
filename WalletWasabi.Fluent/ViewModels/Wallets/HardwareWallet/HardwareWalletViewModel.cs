using WalletWasabi.Gui;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.HardwareWallet
{
	public class HardwareWalletViewModel : WalletViewModel
	{
		internal HardwareWalletViewModel(UiConfig uiConfig, Wallet wallet, WalletManager walletManager) : base(uiConfig, wallet, walletManager)
		{
		}
	}
}
