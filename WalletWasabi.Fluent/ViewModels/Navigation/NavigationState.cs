namespace WalletWasabi.Fluent.ViewModels.Navigation;

public class NavigationState
{
	private NavigationState(
		INavigationStack<RoutableViewModel> homeScreenNavigation,
		INavigationStack<RoutableViewModel> dialogScreenNavigation,
		INavigationStack<RoutableViewModel> fullScreenNavigation,
		INavigationStack<RoutableViewModel> compactDialogScreenNavigation)
	{
		HomeScreenNavigation = homeScreenNavigation;
		DialogScreenNavigation = dialogScreenNavigation;
		FullScreenNavigation = fullScreenNavigation;
		CompactDialogScreenNavigation = compactDialogScreenNavigation;
	}

	public static NavigationState Instance { get; private set; } = null!;

	public INavigationStack<RoutableViewModel> HomeScreenNavigation { get; }

	public INavigationStack<RoutableViewModel> DialogScreenNavigation { get; }

	public INavigationStack<RoutableViewModel> FullScreenNavigation { get; }

	public INavigationStack<RoutableViewModel> CompactDialogScreenNavigation { get; }

	public static void Register(
		INavigationStack<RoutableViewModel> homeScreenNavigation,
		INavigationStack<RoutableViewModel> dialogScreenNavigation,
		INavigationStack<RoutableViewModel> fullScreenNavigation,
		INavigationStack<RoutableViewModel> compactDialogScreenNavigation)
	{
		Instance = new NavigationState(
			homeScreenNavigation,
			dialogScreenNavigation,
			fullScreenNavigation,
			compactDialogScreenNavigation);
	}
}
