using ReactiveUI;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels
{
	public class ClosedHardwareWalletViewModel : ClosedWalletViewModel
	{
		internal ClosedHardwareWalletViewModel(IScreen screen, WalletManager walletManager, Wallet wallet) : base(screen, walletManager, wallet)
		{
		}
	}
}
