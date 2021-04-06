using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Gui;
using WalletWasabi.Stores;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.HardwareWallet
{
	public class HardwareWalletViewModel : WalletViewModel
	{
		internal HardwareWalletViewModel(UiConfig uiConfig, Wallet wallet, BitcoinStore bitcoinStore) : base(uiConfig, wallet, bitcoinStore)
		{
		}
	}
}
