using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Actions
{
	[NavigationMetaData(
		Title = "Receive",
		Caption = "",
		IconName = "web_asset_regular",
		NavBarPosition = NavBarPosition.None,
		NavigationTarget = NavigationTarget.HomeScreen)]
	public partial class ReceiveWalletActionViewModel : WalletActionViewModel
	{
		public ReceiveWalletActionViewModel(Wallet wallet) : base(wallet)
		{
		}
	}
}