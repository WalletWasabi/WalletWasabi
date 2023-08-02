using ReactiveUI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Advanced;

[NavigationMetaData(
	Title = "Wallet Info",
	Caption = "Display wallet info",
	IconName = "nav_wallet_24_regular",
	Order = 4,
	Category = "Wallet",
	Keywords = new[] { "Wallet", "Info", },
	NavBarPosition = NavBarPosition.None,
	NavigationTarget = NavigationTarget.DialogScreen)]
public partial class WalletInfoViewModel : RoutableViewModel
{
	[AutoNotify] private bool _showSensitiveData;
	[AutoNotify] private string _showButtonText = "Show sensitive data";
	[AutoNotify] private string _lockIconString = "eye_show_regular";

	private WalletInfoViewModel(IWalletModel wallet)
	{
		Model = wallet.GetWalletInfo();
		IsHardwareWallet = wallet.IsHardwareWallet;

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableCancel = !wallet.IsWatchOnlyWallet;

		NextCommand = ReactiveCommand.Create(() => Navigate().Clear());

		CancelCommand = ReactiveCommand.Create(() =>
		{
			ShowSensitiveData = !ShowSensitiveData;
			ShowButtonText = ShowSensitiveData ? "Hide sensitive data" : "Show sensitive data";
			LockIconString = ShowSensitiveData ? "eye_hide_regular" : "eye_show_regular";
		});
	}

	public IWalletInfoModel Model { get; }

	public bool IsHardwareWallet { get; }
}
