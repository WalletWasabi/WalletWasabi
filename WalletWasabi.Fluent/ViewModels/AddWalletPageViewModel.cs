using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels
{
	public class AddWalletPageViewModel : NavBarItemViewModel
	{
		public AddWalletPageViewModel(NavigationStateViewModel navigationState) : base(navigationState)
		{
			Title = "Add Wallet";
		}

		public override string IconName => "add_circle_regular";
	}
}
