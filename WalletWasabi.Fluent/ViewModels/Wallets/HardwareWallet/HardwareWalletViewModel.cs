using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Gui;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.HardwareWallet
{
	public class HardwareWalletViewModel : WalletViewModel
	{
		internal HardwareWalletViewModel(Config config, UiConfig uiConfig, Wallet wallet) : base(config, uiConfig, wallet)
		{
		}
	}
}
