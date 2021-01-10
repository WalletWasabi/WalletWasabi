using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Actions
{
	[NavigationMetaData(
		Title = "CoinJoin",
		Caption = "",
		IconName = "wallet_action_coinjoin",
		NavBarPosition = NavBarPosition.None,
		NavigationTarget = NavigationTarget.HomeScreen)]
	public partial class CoinJoinWalletActionViewModel : WalletActionViewModel
	{
		public CoinJoinWalletActionViewModel(Wallet wallet) : base(wallet)
		{
		}
	}
}