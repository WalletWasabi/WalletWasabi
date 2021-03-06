namespace WalletWasabi.Fluent.ViewModels.Navigation
{
	public class NavigationState
	{
		private NavigationState(
			INavigationStack<RoutableViewModel> homeScreenNavigation,
			INavigationStack<RoutableViewModel> dialogScreenNavigation,
			INavigationStack<RoutableViewModel> fullScreenNavigation,
			INavigationStack<RoutableViewModel> miniDialogScreenNavigation)
		{
			HomeScreenNavigation = homeScreenNavigation;
			DialogScreenNavigation = dialogScreenNavigation;
			FullScreenNavigation = fullScreenNavigation;
			MiniDialogScreenNavigation = miniDialogScreenNavigation;
		}

		public static NavigationState Instance { get; private set; } = null!;

		public INavigationStack<RoutableViewModel> HomeScreenNavigation { get; }

		public INavigationStack<RoutableViewModel> DialogScreenNavigation { get; }

		public INavigationStack<RoutableViewModel> FullScreenNavigation { get; }

		public INavigationStack<RoutableViewModel> MiniDialogScreenNavigation { get; }

		public static void Register(
			INavigationStack<RoutableViewModel> homeScreenNavigation,
			INavigationStack<RoutableViewModel> dialogScreenNavigation,
			INavigationStack<RoutableViewModel> fullScreenNavigation,
			INavigationStack<RoutableViewModel> miniDialogScreenNavigation)
		{
			Instance = new NavigationState(
				homeScreenNavigation,
				dialogScreenNavigation,
				fullScreenNavigation,
				miniDialogScreenNavigation);
		}
	}
}
