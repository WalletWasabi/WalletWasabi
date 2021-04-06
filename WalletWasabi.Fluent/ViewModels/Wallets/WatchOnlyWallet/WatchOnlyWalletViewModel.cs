using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Gui;
using WalletWasabi.Stores;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.WatchOnlyWallet
{
	public class WatchOnlyWalletViewModel : WalletViewModel
	{
		internal WatchOnlyWalletViewModel(UiConfig uiConfig, Wallet wallet, BitcoinStore bitcoinStore) : base(uiConfig, wallet, bitcoinStore)
		{
		}
	}
}
