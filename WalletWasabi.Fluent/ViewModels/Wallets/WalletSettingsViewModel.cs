using System.Reactive.Linq;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

[NavigationMetaData(
	Title = "Wallet Settings",
	Caption = "Display wallet settings",
	IconName = "nav_wallet_24_regular",
	Order = 2,
	Category = "Wallet",
	Keywords = new[] { "Wallet", "Settings", },
	NavBarPosition = NavBarPosition.None,
	NavigationTarget = NavigationTarget.DialogScreen)]
public partial class WalletSettingsViewModel : RoutableViewModel
{
	private readonly Wallet _wallet;
	[AutoNotify] private bool _preferPsbtWorkflow;

	public WalletSettingsViewModel(WalletViewModel walletViewModelBase)
	{
		_wallet = walletViewModelBase.Wallet;
		Title = $"{_wallet.WalletName} - Wallet Settings";
		_preferPsbtWorkflow = _wallet.KeyManager.PreferPsbtWorkflow;
		IsHardwareWallet = _wallet.KeyManager.IsHardwareWallet;
		IsWatchOnly = _wallet.KeyManager.IsWatchOnly;

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);

		NextCommand = CancelCommand;

		VerifyRecoveryWordsCommand =
			ReactiveCommand.Create(() => Navigate().To(new VerifyRecoveryWordsViewModel(_wallet)));

		this.WhenAnyValue(x => x.PreferPsbtWorkflow)
			.Skip(1)
			.Subscribe(
				value =>
				{
					_wallet.KeyManager.PreferPsbtWorkflow = value;
					_wallet.KeyManager.ToFile();
					walletViewModelBase.RaisePropertyChanged(nameof(walletViewModelBase.PreferPsbtWorkflow));
				});
	}

	public bool IsHardwareWallet { get; }

	public bool IsWatchOnly { get; }

	public ICommand VerifyRecoveryWordsCommand { get; }
}
