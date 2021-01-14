using WalletWasabi.Services;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.WatchOnlyWallet
{
	public class ClosedWatchOnlyWalletViewModel : ClosedWalletViewModel
	{
		internal ClosedWatchOnlyWalletViewModel(WalletManager walletManager, Wallet wallet, LegalChecker legalChecker) : base(walletManager, wallet, legalChecker)
		{
		}
	}
}
