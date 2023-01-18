using CommunityToolkit.Mvvm.ComponentModel;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Login.PasswordFinder;

[NavigationMetaData(Title = "Password Finder")]
public partial class PasswordFoundViewModel : RoutableViewModel
{
	[ObservableProperty] private string _password;
	[ObservableProperty] private bool _success;

	public PasswordFoundViewModel(string password)
	{
		_password = password;

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: false);

		EnableBack = false;

		NextCommand = CancelCommand;
	}
}
