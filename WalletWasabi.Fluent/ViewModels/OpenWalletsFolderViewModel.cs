using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels
{
	[NavigationMetaData(
		Title = "Wallets Folder",
		Caption = "Open the wallets folder",
		Order = 6,
		Category = "General",
		IconName = "open_regular",
		Keywords = new[] { "Open", "Wallets", "Folder" },
		NavigationTarget = NavigationTarget.Default)]
	public partial class OpenWalletsFolderViewModel : RoutableViewModel
	{
	}
}