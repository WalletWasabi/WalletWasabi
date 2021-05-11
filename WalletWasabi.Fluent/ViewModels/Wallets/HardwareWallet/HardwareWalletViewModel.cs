using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Gui;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Fluent.ViewModels.Wallets.HardwareWallet
{
	public class HardwareWalletViewModel : WalletViewModel
	{
		internal HardwareWalletViewModel(
			WalletManager walletManager,
			TransactionBroadcaster transactionBroadcaster,
			Config config,
			UiConfig uiConfig,
			HttpClientFactory clientFactory,
			Wallet wallet)
			: base(walletManager,
				transactionBroadcaster,
				config,
				uiConfig,
				clientFactory,
				wallet)
		{
		}
	}
}