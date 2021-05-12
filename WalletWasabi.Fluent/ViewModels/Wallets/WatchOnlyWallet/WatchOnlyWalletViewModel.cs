using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Gui;
using WalletWasabi.Stores;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Fluent.ViewModels.Wallets.WatchOnlyWallet
{
	public class WatchOnlyWalletViewModel : WalletViewModel
	{
		internal WatchOnlyWalletViewModel(
			WalletManager walletManager,
			TransactionBroadcaster transactionBroadcaster,
			Config config,
			UiConfig uiConfig,
			HttpClientFactory clientFactory,
			Wallet wallet)
			: base(
				walletManager,
				transactionBroadcaster,
				config,
				uiConfig,
				clientFactory,
				wallet)
		{
		}
	}
}