using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Login
{
	public class LoginViewModel : LoginViewModelBase
	{
		public LoginViewModel(WalletViewModelBase wallet, WalletManager walletManager) : base(walletManager)
		{
			SelectedWallet = wallet;
		}
	}
}