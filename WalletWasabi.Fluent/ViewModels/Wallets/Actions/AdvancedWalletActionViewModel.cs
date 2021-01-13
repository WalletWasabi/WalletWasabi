namespace WalletWasabi.Fluent.ViewModels.Wallets.Actions
{
	[NavigationMetaData(
		Title = "Advanced",
		Caption = "",
		IconName = "wallet_action_advanced",
		NavBarPosition = NavBarPosition.None,
		Searchable = false,
		NavigationTarget = NavigationTarget.HomeScreen)]
	public partial class AdvancedWalletActionViewModel : WalletActionViewModel
	{
		public AdvancedWalletActionViewModel(WalletViewModelBase wallet) : base(wallet)
		{
		}

		public string Title => "Advanced";
	}
}