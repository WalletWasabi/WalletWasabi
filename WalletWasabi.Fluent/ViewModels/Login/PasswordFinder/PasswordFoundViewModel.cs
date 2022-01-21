using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Login.PasswordFinder;

[NavigationMetaData(Title = "Password Finder")]
public partial class PasswordFoundViewModel : RoutableViewModel
{
	[AutoNotify] private string _password;
	[AutoNotify] private bool _success;

	public PasswordFoundViewModel(string password)
	{
		_password = password;

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: false);

		EnableBack = false;

		NextCommand = CancelCommand;
	}
}
