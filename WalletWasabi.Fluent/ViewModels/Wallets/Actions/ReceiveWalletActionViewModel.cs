
namespace WalletWasabi.Fluent.ViewModels.Wallets.Actions
{
	[NavigationMetaData(
		Title = "Receive",
		Caption = "",
		IconName = "wallet_action_receive",
		NavBarPosition = NavBarPosition.None,
		NavigationTarget = NavigationTarget.HomeScreen)]
	public partial class ReceiveWalletActionViewModel : WalletActionViewModel
	{
		public ReceiveWalletActionViewModel(WalletViewModelBase wallet) : base(wallet)
		{
		}
	}
}