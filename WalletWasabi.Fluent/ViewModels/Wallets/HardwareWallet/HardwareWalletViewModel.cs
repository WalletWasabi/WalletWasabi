using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Gui;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.HardwareWallet
{
	public class HardwareWalletViewModel : WalletViewModel
	{
		internal HardwareWalletViewModel(UiConfig uiConfig, TransactionBroadcaster broadcaster, Wallet wallet) : base(uiConfig, broadcaster, wallet)
		{
		}
	}
}
