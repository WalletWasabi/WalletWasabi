using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Actions
{
	[NavigationMetaData(
		Title = "Advanced",
		Caption = "",
		IconName = "wallet_action_advanced",
		NavBarPosition = NavBarPosition.None,
		Searchable = false,
		NavigationTarget = NavigationTarget.HomeScreen)]
	public partial class AdvancedWalletActionViewModel : NavBarItemViewModel
	{
		public AdvancedWalletActionViewModel(WalletViewModelBase wallet) : base(NavigationMode.Normal)
		{
			Title = "Advanced";

			SelectionMode = NavBarItemSelectionMode.Selected;
		}
	}
}