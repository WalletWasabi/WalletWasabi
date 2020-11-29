using ReactiveUI;
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
		public static NavigationState Instance { get; private set; }

		private NavigationState(Func<IScreen> homeScreen, Func<IScreen> dialogScreen, Func<IDialogHost> dialogHost)
		{
			HomeScreen = homeScreen;
			DialogScreen = dialogScreen;
			DialogHost = dialogHost;
		}

		public Func<IScreen> HomeScreen { get; }
		public Func<IScreen> DialogScreen { get; }
		public Func<IDialogHost> DialogHost { get; }

		public static void Register(Func<IScreen> homeScreen, Func<IScreen> dialogScreen, Func<IDialogHost> dialogHost)
		{
			Instance = new NavigationState(homeScreen, dialogScreen, dialogHost);
		}
	}
}