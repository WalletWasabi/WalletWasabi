using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Login
{
	public class SimpleLoginViewModel : RoutableViewModel
	{
		public SimpleLoginViewModel(WalletViewModelBase wallet, WalletManager walletManager)
		{
			Login = new LoginViewModel(wallet, walletManager);
			NextCommand = ReactiveCommand.Create(() =>
			{
				Navigate().To(new FullScreenLoginViewModel(walletManager));
			});
		}

		public LoginViewModel Login { get; }
	}
}