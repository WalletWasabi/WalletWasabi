using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Daemon;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Models;

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

	private WalletSettingsViewModel(IWalletModel wallet)
	{
		_wallet = wallet;
		Title = $"{wallet.Name} - Wallet Settings";
		_preferPsbtWorkflow = wallet.Settings.PreferPsbtWorkflow;
		IsHardwareWallet = wallet.IsHardwareWallet;
		IsWatchOnly = wallet.IsWatchOnlyWallet;

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);

		NextCommand = CancelCommand;

		VerifyRecoveryWordsCommand = ReactiveCommand.Create(() => Navigate().To().VerifyRecoveryWords(wallet));

		this.WhenAnyValue(x => x.PreferPsbtWorkflow)
			.Skip(1)
			.Subscribe(value =>
			{
				wallet.Settings.PreferPsbtWorkflow = value;
				wallet.Settings.Save();
			});

		this.ValidateProperty(x => x.WalletName,
			errors =>
			{
				try
				{
					new FileInfo(WalletName);
				}
				catch
				{
					errors.Add(ErrorSeverity.Error, "Invalid path");
				}
			});
	}

	public string WalletName
	{
		get => _wallet.Name;
		set
		{
			try
			{
				new FileInfo(WalletName);
				_wallet.Name = value;
			}
			catch
			{
			}
		}
	}

	public bool IsHardwareWallet { get; }

	public bool IsWatchOnly { get; }

	public ICommand VerifyRecoveryWordsCommand { get; }
}
