using WalletWasabi.Fluent.ViewModels.NavBar;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Actions
{
	[NavigationMetaData(
		Title = "Receive",
		Caption = "",
		IconName = "wallet_action_receive",
		NavBarPosition = NavBarPosition.None,
		Searchable = false,
		NavigationTarget = NavigationTarget.DialogScreen)]
	public partial class ReceiveWalletActionViewModel : NavBarItemViewModel
	{
		public ReceiveWalletActionViewModel(WalletViewModelBase wallet)
		{
			Title = "Receive";
			SelectionMode = NavBarItemSelectionMode.Button;
		}
	}
}