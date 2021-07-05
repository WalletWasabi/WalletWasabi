using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets
{
	[NavigationMetaData(Title="Wallet Settings")]
	public partial class WalletSettingsViewModel : RoutableViewModel
	{
		public WalletSettingsViewModel(WalletViewModel walletViewModel)
		{
			SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);

			NextCommand = CancelCommand;
		}
	}
}
