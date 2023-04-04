using System.Threading.Tasks;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.Navigation;

public static class NavigationExtensions
{
	public static void To<T>(this INavigate navigate, T viewModel, NavigationTarget target = NavigationTarget.Default, NavigationMode mode = NavigationMode.Normal) where T : RoutableViewModel
	{
		target = GetTarget(viewModel, target);
		navigate.Navigate(target).To(viewModel, mode);
	}

	public static async Task ShowErrorAsync(this INavigationStack<RoutableViewModel> navigate, string title, string message, string caption)
	{
		var dialog = new ShowErrorDialogViewModel(message, title, caption);
		await navigate.NavigateDialogAsync(dialog);
	}

	public static NavigationTarget GetTarget(RoutableViewModel viewModel, NavigationTarget target)
	{
		var currentTarget = target;
		if (currentTarget == NavigationTarget.Default)
		{
			currentTarget = viewModel.CurrentTarget;

			if (currentTarget == NavigationTarget.Default)
			{
				currentTarget = viewModel.DefaultTarget;
			}
		}

		return currentTarget;
	}
}
