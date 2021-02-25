using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Gui;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.WatchOnlyWallet
{
	public class WatchOnlyWalletViewModel : WalletViewModel
	{
		internal WatchOnlyWalletViewModel(UiConfig uiConfig, TransactionBroadcaster broadcaster, Wallet wallet) : base(uiConfig, broadcaster, wallet)
		{
		}
	}
}