using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.WalletPages
{
	[NavigationMetaData(
		Title = "Send",
		Caption = "",
		IconName = "web_asset_regular",
		Order = 0,
		Category = "Wallet",
		Keywords = new string[]
		{
		},
		NavBarPosition = NavBarPosition.None,
		NavigationTarget = NavigationTarget.HomeScreen)]
	public partial class SendWalletViewModel : WalletPageViewModel
	{
		public SendWalletViewModel(Wallet wallet) : base(wallet)
		{
		}
	}
}