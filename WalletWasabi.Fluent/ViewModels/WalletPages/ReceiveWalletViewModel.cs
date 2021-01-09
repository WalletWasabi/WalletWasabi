using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.WalletPages
{
	[NavigationMetaData(
		Title = "Receive",
		Caption = "",
		IconName = "web_asset_regular",
		Order = 1,
		Category = "Wallet",
		Keywords = new string[]
		{
		},
		NavBarPosition = NavBarPosition.None,
		NavigationTarget = NavigationTarget.HomeScreen)]
	public partial class ReceiveWalletViewModel : WalletPageViewModel
	{
		public ReceiveWalletViewModel(Wallet wallet) : base(wallet)
		{
		}
	}
}