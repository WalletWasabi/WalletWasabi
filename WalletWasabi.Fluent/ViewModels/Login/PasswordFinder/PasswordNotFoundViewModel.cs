using ReactiveUI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Login.PasswordFinder;

[NavigationMetaData(Title = "Password Finder")]
public partial class PasswordNotFoundViewModel : RoutableViewModel
{
	private PasswordNotFoundViewModel(IWalletModel wallet)
	{
		NextCommand = ReactiveCommand.Create(() => OnNext(wallet));

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	private void OnNext(IWalletModel wallet)
	{
		var page = new PasswordFinderIntroduceViewModel(UiContext, wallet);
		UiContext.Navigate().To(page, mode: NavigationMode.Clear);
		if (page.NextCommand is { } cmd)
		{
			cmd.Execute(default);
		}
	}
}
