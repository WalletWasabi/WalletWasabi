using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Login
{
	public class LoginViewModel : RoutableViewModel
	{
		public LoginViewModel(WalletViewModelBase wallet)
		{
			NextCommand = ReactiveCommand.Create(() =>
			{
				wallet.IsLoggedIn = true;
				wallet.OpenCommand
			});
		}
	}
}