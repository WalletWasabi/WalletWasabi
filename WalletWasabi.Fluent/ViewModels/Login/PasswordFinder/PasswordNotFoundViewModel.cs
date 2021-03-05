using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Login.PasswordFinder
{
	[NavigationMetaData(Title = "Password Finder")]
	public partial class PasswordNotFoundViewModel : RoutableViewModel
	{
		public PasswordNotFoundViewModel(Wallet wallet)
		{
			NextCommand = ReactiveCommand.Create(() => NextExecute(wallet));
		}

		private void NextExecute(Wallet wallet)
		{
			var page = new PasswordFinderIntroduceViewModel(wallet);
			Navigate().To(page, NavigationMode.Clear);
			if (page.NextCommand is { } cmd)
			{
				cmd.Execute(default);
			}
		}
	}
}