using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.HardwareWallet
{
	public class ClosedHardwareWalletViewModel : ClosedWalletViewModel
	{
		internal ClosedHardwareWalletViewModel(WalletManagerViewModel walletManager, Wallet wallet) : base(walletManager, wallet)
		{
		}
	}
}
