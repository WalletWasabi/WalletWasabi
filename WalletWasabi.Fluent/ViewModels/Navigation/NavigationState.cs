using WalletWasabi.Fluent.Models.UI;

namespace WalletWasabi.Fluent.ViewModels.Navigation;

public class NavigationState
{
	public NavigationState(
		UIContext uiContext,
		INavigationStack<RoutableViewModel> homeScreenNavigation,
		INavigationStack<RoutableViewModel> dialogScreenNavigation,
		INavigationStack<RoutableViewModel> fullScreenNavigation,
		INavigationStack<RoutableViewModel> compactDialogScreenNavigation)
	{
		UIContext = uiContext;
		HomeScreenNavigation = homeScreenNavigation;
		DialogScreenNavigation = dialogScreenNavigation;
		FullScreenNavigation = fullScreenNavigation;
		CompactDialogScreenNavigation = compactDialogScreenNavigation;
	}

	public UIContext UIContext { get; }

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
}
