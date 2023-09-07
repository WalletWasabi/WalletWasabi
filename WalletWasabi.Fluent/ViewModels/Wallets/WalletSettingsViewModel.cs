using System.Linq;
using System.Reactive.Linq;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;

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
	private readonly IWalletModel _wallet;
	[AutoNotify] private bool _preferPsbtWorkflow;

	private WalletSettingsViewModel(IWalletModel walletModel)
	{
		_wallet = walletModel;
		Title = $"{_wallet.Name} - Wallet Settings";
		_preferPsbtWorkflow = _wallet.Settings.PreferPsbtWorkflow;
		IsHardwareWallet = _wallet.IsHardwareWallet;
		IsWatchOnly = _wallet.IsWatchOnlyWallet;

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);

		NextCommand = CancelCommand;

		// TODO: Finish partial refactor
		// this must be removed after VerifyRecoveryWords has been decoupled
		var wallet = Services.WalletManager.GetWallets(false).First(x => x.WalletName == walletModel.Id);
		VerifyRecoveryWordsCommand = ReactiveCommand.Create(() => Navigate().To().VerifyRecoveryWords(wallet));

		this.WhenAnyValue(x => x.PreferPsbtWorkflow)
			.Skip(1)
			.Subscribe(value =>
			{
				_wallet.Settings.PreferPsbtWorkflow = value;
				_wallet.Settings.Save();
			});
	}

	public string WalletName
	{
		get => _wallet.Name;
		set => _wallet.Name = value;
	}

	public bool IsHardwareWallet { get; }

	public bool IsWatchOnly { get; }

	public ICommand VerifyRecoveryWordsCommand { get; }
}
