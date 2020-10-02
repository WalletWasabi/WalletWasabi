using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels
{
	public class HomePageViewModel : NavBarItemViewModel
	{
		public override string IconName => "home_regular";

		public HomePageViewModel(IScreen screen) : base(screen)
		{
			Title = "Home";
		}
	}
}
