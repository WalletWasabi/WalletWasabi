using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Login
{
	public partial class FullScreenLoginViewModel : RoutableViewModel
	{
		public FullScreenLoginViewModel(WalletManager walletManager)
		{
			// Login = new LoginViewModel(wallet, walletManager);
		}

		public LoginViewModel Login { get; }
	}
}