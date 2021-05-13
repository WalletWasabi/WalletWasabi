using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Gui;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets
{
	public class WatchOnlyWalletViewModel : WalletViewModel
	{
		internal WatchOnlyWalletViewModel(Wallet wallet)
			: base(wallet)
		{
		}
	}
}
