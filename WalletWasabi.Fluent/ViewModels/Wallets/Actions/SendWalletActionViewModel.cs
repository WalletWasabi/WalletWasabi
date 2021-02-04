namespace WalletWasabi.Fluent.ViewModels.Wallets.Actions
{
	[NavigationMetaData(
		Title = "Send",
		Caption = "",
		IconName = "wallet_action_send",
		NavBarPosition = NavBarPosition.None,
		Searchable = false,
		NavigationTarget = NavigationTarget.HomeScreen)]
	public partial class SendWalletActionViewModel : WalletActionViewModel
	{
		public SendWalletActionViewModel(WalletViewModelBase wallet) : base(wallet)
		{
			Title = "Send";
		}
	}
}