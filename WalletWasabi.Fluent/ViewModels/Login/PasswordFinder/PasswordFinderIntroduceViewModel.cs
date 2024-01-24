using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Login.PasswordFinder;

[NavigationMetaData(Title = "Password Finder")]
public partial class PasswordFinderIntroduceViewModel : RoutableViewModel
{
	private PasswordFinderIntroduceViewModel(IWalletModel wallet)
	{
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;

		NextCommand = ReactiveCommand.CreateFromTask(async () => await OnNextAsync(wallet));
	}

	private async Task OnNextAsync(IWalletModel wallet)
	{
		var dialogResult = await NavigateDialogAsync(
			new CreatePasswordDialogViewModel("Password", "Type in your most likely password", enableEmpty: false),
			NavigationTarget.CompactDialogScreen);

		if (dialogResult.Result is { } password)
		{
			var passwordFinder = wallet.Auth.GetPasswordFinder(password);
			Navigate().To().SelectCharset(passwordFinder);
		}
	}
}
