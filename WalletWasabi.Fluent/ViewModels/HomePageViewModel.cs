using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels
{
	public class HomePageViewModel : NavBarItemViewModel
	{
		public HomePageViewModel(NavigationStateViewModel navigationState) : base(navigationState)
		{
			Title = "Home";
		}

		public override string IconName => "home_regular";
	}
}
