
namespace WalletWasabi.Fluent.ViewModels.Wallets.Actions
{
	[NavigationMetaData(
		Title = "Send",
		Caption = "",
		IconName = "wallet_action_send",
		NavBarPosition = NavBarPosition.None,
		NavigationTarget = NavigationTarget.HomeScreen)]
	public partial class SendWalletActionViewModel : WalletActionViewModel
	{
		public SendWalletActionViewModel(WalletViewModelBase wallet) : base(wallet)
		{
		}
	}
}