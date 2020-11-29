using System;
using WalletWasabi.Fluent.ViewModels.Dialogs;

namespace WalletWasabi.Fluent.ViewModels.Navigation
{
	public enum NavigationTarget
	{
		Default = 0,
		HomeScreen = 1,
		DialogScreen = 2,
		DialogHost = 3
	}

	public class NavigationState
	{
		private NavigationState(INavigationManager<RoutableViewModel> homeScreenNavigation, INavigationManager<RoutableViewModel> dialogScreenNavigation, Func<IDialogHost> dialogHost)
		{
			HomeScreenNavigation = homeScreenNavigation;
			DialogScreenNavigation = dialogScreenNavigation;
			DialogHost = dialogHost;
		}

		public static NavigationState Instance { get; private set; }
		public Func<IDialogHost> DialogHost { get; }

		public INavigationManager<RoutableViewModel> HomeScreenNavigation { get; }

		public INavigationManager<RoutableViewModel> DialogScreenNavigation { get; }

		public static void Register(INavigationManager<RoutableViewModel> homeScreenNavigation, INavigationManager<RoutableViewModel> dialogScreenNavigation, Func<IDialogHost> dialogHost)
		{
			Instance = new NavigationState(homeScreenNavigation, dialogScreenNavigation, dialogHost);
		}
	}
}