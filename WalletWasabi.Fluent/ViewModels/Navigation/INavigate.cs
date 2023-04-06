using System.Threading.Tasks;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.Navigation;

public interface INavigate
{
	INavigationStack<RoutableViewModel> Navigate(NavigationTarget target);

	FluentNavigate To();

	Task<DialogResult<TResult>> NavigateDialogAsync<TResult>(DialogViewModelBase<TResult> dialog, NavigationTarget target = NavigationTarget.Default, NavigationMode navigationMode = NavigationMode.Normal);
}
