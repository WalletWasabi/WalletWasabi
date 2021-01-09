using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Actions
{
	[NavigationMetaData(
		Title = "Advanced",
		Caption = "",
		IconName = "web_asset_regular",
		NavBarPosition = NavBarPosition.None,
		NavigationTarget = NavigationTarget.HomeScreen)]
	public partial class AdvancedWalletActionViewModel : WalletActionViewModel
	{
		public AdvancedWalletActionViewModel(Wallet wallet) : base(wallet)
		{
		}
	}
}