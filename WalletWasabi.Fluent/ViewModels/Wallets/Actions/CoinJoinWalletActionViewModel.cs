using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Actions
{
	[NavigationMetaData(
		Title = "CoinJoin",
		Caption = "",
		IconName = "web_asset_regular",
		NavBarPosition = NavBarPosition.None,
		NavigationTarget = NavigationTarget.HomeScreen)]
	public partial class CoinJoinWalletActionViewModel : WalletActionViewModel
	{
		public CoinJoinWalletActionViewModel(Wallet wallet) : base(wallet)
		{
		}
	}
}