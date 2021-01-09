using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.WalletPages
{
	[NavigationMetaData(
		Title = "Advanced",
		Caption = "",
		IconName = "web_asset_regular",
		Order = 3,
		Category = "Wallet",
		Keywords = new string[]
		{
		},
		NavBarPosition = NavBarPosition.None,
		NavigationTarget = NavigationTarget.HomeScreen)]
	public partial class AdvancedWalletViewModel : WalletPageViewModel
	{
		public AdvancedWalletViewModel(Wallet wallet) : base(wallet)
		{
		}
	}
}