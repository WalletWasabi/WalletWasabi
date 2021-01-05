using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Login.PasswordFinder
{
	public class PasswordFinderIntroduceViewModel : RoutableViewModel
	{
		public PasswordFinderIntroduceViewModel(Wallet wallet)
		{
			Title = "Password Finder";

			NextCommand = ReactiveCommand.Create(() =>
			{
				var options = new PasswordFinderOptions(wallet);
				Navigate().To(new SelectCharsetViewModel(options));
			});
		}
	}
}