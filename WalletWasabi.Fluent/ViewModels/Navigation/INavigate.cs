namespace WalletWasabi.Fluent.ViewModels.Navigation;

public interface INavigate
{
	INavigationStack<RoutableViewModel> Navigate(NavigationTarget target);

	FluentNavigate To();
}
