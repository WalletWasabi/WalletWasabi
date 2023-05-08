using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Login.PasswordFinder;

[NavigationMetaData(Title = "Password Finder")]
public partial class ContainsSymbolsViewModel : RoutableViewModel
{
	private ContainsSymbolsViewModel(IPasswordFinderModel model)
	{
		Options = model;

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = true;

		YesCommand = ReactiveCommand.Create(() => SetAnswer(true));
		NoCommand = ReactiveCommand.Create(() => SetAnswer(false));
	}

	public IPasswordFinderModel Options { get; }

	public ICommand YesCommand { get; }

	public ICommand NoCommand { get; }

	private void SetAnswer(bool ans)
	{
		Options.UseSymbols = ans;
		UiContext.Navigate().To().SearchPassword(Options);
	}
}
