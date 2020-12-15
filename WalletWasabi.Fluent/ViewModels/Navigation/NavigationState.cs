using System;
using WalletWasabi.Fluent.ViewModels.Dialogs;

namespace WalletWasabi.Fluent.ViewModels.Navigation
{
	public class NavigationState
	{
		private NavigationState(
			INavigationStack<RoutableViewModel> homeScreenNavigation,
			INavigationStack<RoutableViewModel> dialogScreenNavigation,
			INavigationStack<RoutableViewModel> fullScreenNavigation,
			Func<IDialogHost> dialogHost)
		{
			HomeScreenNavigation = homeScreenNavigation;
			DialogScreenNavigation = dialogScreenNavigation;
			FullScreenNavigation = fullScreenNavigation;
			DialogHost = dialogHost;
		}

		public static NavigationState Instance { get; private set; } = null!;

		public Func<IDialogHost> DialogHost { get; }

		public INavigationStack<RoutableViewModel> HomeScreenNavigation { get; }

		public INavigationStack<RoutableViewModel> DialogScreenNavigation { get; }

		public INavigationStack<RoutableViewModel> FullScreenNavigation { get; }

		public static void Register(
			INavigationStack<RoutableViewModel> homeScreenNavigation,
			INavigationStack<RoutableViewModel> dialogScreenNavigation,
			INavigationStack<RoutableViewModel> fullScreenNavigation,
			Func<IDialogHost> dialogHost)
		{
			Instance = new NavigationState(
				homeScreenNavigation,
				dialogScreenNavigation,
				fullScreenNavigation,
				dialogHost);
		}
	}
}