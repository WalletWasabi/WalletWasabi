using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Actions
{
	[NavigationMetaData(
		Title = "Send",
		Caption = "",
		IconName = "web_asset_regular",
		NavBarPosition = NavBarPosition.None,
		NavigationTarget = NavigationTarget.HomeScreen)]
	public partial class SendWalletActionViewModel : WalletActionViewModel
	{
		public SendWalletActionViewModel(Wallet wallet) : base(wallet)
		{
		}
	}
}