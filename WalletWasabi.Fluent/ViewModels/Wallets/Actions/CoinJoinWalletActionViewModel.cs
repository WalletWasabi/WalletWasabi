using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Actions
{
	[NavigationMetaData(
		Title = "CoinJoin",
		Caption = "",
		IconName = "wallet_action_coinjoin",
		NavBarPosition = NavBarPosition.None,
		Searchable = false,
		NavigationTarget = NavigationTarget.HomeScreen)]
	public partial class CoinJoinWalletActionViewModel : NavBarItemViewModel
	{
		public CoinJoinWalletActionViewModel(WalletViewModelBase wallet) : base(NavigationMode.Normal)
		{
			Title = "CoinJoin";
		}
	}
}