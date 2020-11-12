using ReactiveUI;
using System;
using WalletWasabi.Fluent.ViewModels.Dialogs;

namespace WalletWasabi.Fluent.ViewModels
{
	public enum NavigationTarget
	{
		Default = 0,
		Home = 1,
		Dialog = 2
	}

	public class NavigationStateViewModel
	{
		public NavigationStateViewModel(Func<IScreen> homeScreen, Func<IScreen> dialogScreen, Func<IDialogHost> dialogHost)
		{
			HomeScreen = homeScreen;
			DialogScreen = dialogScreen;
			DialogHost = dialogHost;
		}
		public Func<IScreen> HomeScreen { get; }
		public Func<IScreen> DialogScreen { get; }
		public Func<IDialogHost> DialogHost { get; }
	}
}