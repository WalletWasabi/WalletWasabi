using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Login.PasswordFinder
{
	public class PasswordNotFoundViewModel : RoutableViewModel
	{
		public PasswordNotFoundViewModel(Wallet wallet)
		{
			Title = "Password Finder";

			NextCommand = ReactiveCommand.Create(() =>
			{
				var page = new PasswordFinderIntroduceViewModel(wallet);
				Navigate().To(page, NavigationMode.Clear);
				if (page.NextCommand is { } cmd)
				{
					cmd.Execute(default);
				}
			});
		}
	}
}