using ReactiveUI;
using System.Reactive.Linq;
using System.Threading.Tasks;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.Navigation;

public class NavigationState : ReactiveObject, INavigate
{
	public NavigationState(
		UiContext uiContext,
		INavigationStack<RoutableViewModel> homeScreenNavigation,
		INavigationStack<RoutableViewModel> dialogScreenNavigation,
		INavigationStack<RoutableViewModel> fullScreenNavigation,
		INavigationStack<RoutableViewModel> compactDialogScreenNavigation)
	{
		UiContext = uiContext;
		HomeScreenNavigation = homeScreenNavigation;
		DialogScreenNavigation = dialogScreenNavigation;
		FullScreenNavigation = fullScreenNavigation;
		CompactDialogScreenNavigation = compactDialogScreenNavigation;

		this.WhenAnyValue(
				x => x.DialogScreenNavigation.CurrentPage,
				x => x.CompactDialogScreenNavigation.CurrentPage,
				x => x.FullScreenNavigation.CurrentPage,
				x => x.HomeScreenNavigation.CurrentPage,
				(dialog, compactDialog, fullScreenDialog, mainScreen) => compactDialog ?? dialog ?? fullScreenDialog ?? mainScreen)
			.WhereNotNull()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Do(OnCurrentPageChanged)
			.Subscribe();
	}

	public UiContext UiContext { get; }

	public INavigationStack<RoutableViewModel> HomeScreenNavigation { get; }

	public INavigationStack<RoutableViewModel> DialogScreenNavigation { get; }

	public INavigationStack<RoutableViewModel> FullScreenNavigation { get; }

	public INavigationStack<RoutableViewModel> CompactDialogScreenNavigation { get; }

	public INavigationStack<RoutableViewModel> Navigate(NavigationTarget currentTarget)
	{
		return currentTarget switch
		{
			NavigationTarget.HomeScreen => HomeScreenNavigation,
			NavigationTarget.DialogScreen => DialogScreenNavigation,
			NavigationTarget.FullScreen => FullScreenNavigation,
			NavigationTarget.CompactDialogScreen => CompactDialogScreenNavigation,
			_ => throw new NotSupportedException(),
		};
	}

	public FluentNavigate To()
	{
		return new FluentNavigate(UiContext);
	}

	private void OnCurrentPageChanged(RoutableViewModel page)
	{
		if (HomeScreenNavigation.CurrentPage is { } homeScreen)
		{
			homeScreen.IsActive = false;
		}

		if (DialogScreenNavigation.CurrentPage is { } dialogScreen)
		{
			dialogScreen.IsActive = false;
		}

		if (FullScreenNavigation.CurrentPage is { } fullScreen)
		{
			fullScreen.IsActive = false;
		}

		if (CompactDialogScreenNavigation.CurrentPage is { } compactDialogScreen)
		{
			compactDialogScreen.IsActive = false;
		}

		page.IsActive = true;
	}

	public async Task<DialogResult<TResult>> NavigateDialogAsync<TResult>(DialogViewModelBase<TResult> dialog, NavigationTarget target = NavigationTarget.Default, NavigationMode navigationMode = NavigationMode.Normal)
	{
		target = NavigationExtensions.GetTarget(dialog, target);
		return await Navigate(target).NavigateDialogAsync(dialog, navigationMode);
	}
}
