using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.WalletPages
{
	[NavigationMetaData(
		Title = "CoinJoin",
		Caption = "",
		IconName = "web_asset_regular",
		Order = 2,
		Category = "Wallet",
		Keywords = new string[]
		{
		},
		NavBarPosition = NavBarPosition.None,
		NavigationTarget = NavigationTarget.HomeScreen)]
	public partial class CoinJoinWalletViewModel : WalletPageViewModel
	{
		public CoinJoinWalletViewModel(Wallet wallet) : base(wallet)
		{
		}
	}
}