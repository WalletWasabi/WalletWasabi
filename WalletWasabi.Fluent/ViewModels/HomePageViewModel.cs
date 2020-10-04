using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels
{
	public class HomePageViewModel : NavBarItemViewModel
	{
		public HomePageViewModel(IScreen screen) : base(screen)
		{
			Title = "Home";
		}

		public override string IconName => "home_regular";
	}
}
