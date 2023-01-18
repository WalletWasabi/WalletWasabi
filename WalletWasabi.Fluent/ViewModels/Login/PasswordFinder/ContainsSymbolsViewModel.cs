using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets.PasswordFinder;

namespace WalletWasabi.Fluent.ViewModels.Login.PasswordFinder;

[NavigationMetaData(Title = "Password Finder")]
public partial class ContainsSymbolsViewModel : RoutableViewModel
{
	public ContainsSymbolsViewModel(PasswordFinderOptions options)
	{
		Options = options;

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = true;

		YesCommand = new RelayCommand(() => SetAnswer(true));
		NoCommand = new RelayCommand(() => SetAnswer(false));
	}

	public PasswordFinderOptions Options { get; }

	public ICommand YesCommand { get; }

	public ICommand NoCommand { get; }

	private void SetAnswer(bool ans)
	{
		Options.UseSymbols = ans;
		Navigate().To(new SearchPasswordViewModel(Options));
	}
}
