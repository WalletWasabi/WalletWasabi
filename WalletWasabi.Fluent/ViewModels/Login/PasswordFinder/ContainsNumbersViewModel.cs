using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Login.PasswordFinder;

[NavigationMetaData(Title = "Password Finder")]
public partial class ContainsNumbersViewModel : RoutableViewModel
{
	private ContainsNumbersViewModel(IPasswordFinderModel model)
	{
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = true;

		YesCommand = ReactiveCommand.Create(() => SetAnswer(model, true));
		NoCommand = ReactiveCommand.Create(() => SetAnswer(model, false));
	}

	public ICommand YesCommand { get; }

	public ICommand NoCommand { get; }

	private void SetAnswer(IPasswordFinderModel model, bool ans)
	{
		model.UseNumbers = ans;
		UiContext.Navigate().To().ContainsSymbols(model);
	}
}
