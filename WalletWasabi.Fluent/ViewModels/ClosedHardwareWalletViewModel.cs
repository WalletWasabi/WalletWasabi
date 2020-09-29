using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels
{
	public class ClosedHardwareWalletViewModel : ClosedWalletViewModel
	{
		internal ClosedHardwareWalletViewModel(WalletManager walletManager, Wallet wallet) : base(walletManager, wallet)
		{
		}
	}
}
