using System.Threading.Tasks;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.Navigation;

public static class NavigationExtensions
{
	public static void To<T>(this INavigate navigate, T viewModel, NavigationTarget target = NavigationTarget.Default, NavigationMode mode = NavigationMode.Normal) where T : RoutableViewModel
	{
		target = GetTarget(viewModel, target);
		navigate.Navigate(target).To(viewModel, mode);
	}

	public static async Task<DialogResult<TResult>> NavigateDialogAsync<TResult>(this INavigate navigate, DialogViewModelBase<TResult> dialog, NavigationTarget target = NavigationTarget.Default, NavigationMode navigationMode = NavigationMode.Normal)
	{
		target = GetTarget(dialog, target);
		return await navigate.Navigate(target).NavigateDialogAsync(dialog, navigationMode);
	}

	public static async Task<DialogResult<TResult>> NavigateDialogAsync<TResult>(this INavigationStack<RoutableViewModel> navigate, DialogViewModelBase<TResult> dialog, NavigationMode navigationMode = NavigationMode.Normal)
	{
		var dialogTask = dialog.GetDialogResultAsync();

		navigate.To(dialog, navigationMode);

		var result = await dialogTask;

		navigate.Back();

		return result;
	}

	private static NavigationTarget GetTarget(RoutableViewModel viewModel, NavigationTarget target)
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
