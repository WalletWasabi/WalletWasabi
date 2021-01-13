using WalletWasabi.Fluent.ViewModels.NavBar;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Actions
{
	[NavigationMetaData(
		Title = "Receive",
		Caption = "",
		IconName = "wallet_action_receive",
		NavBarPosition = NavBarPosition.None,
		Searchable = false,
		NavigationTarget = NavigationTarget.HomeScreen)]
	public partial class ReceiveWalletActionViewModel : WalletActionViewModel
	{
		public ReceiveWalletActionViewModel(WalletViewModelBase wallet) : base(wallet)
		{
			Title = "Receive";
		}
	}
}