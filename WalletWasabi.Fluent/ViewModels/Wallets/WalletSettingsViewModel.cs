using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Daemon;
using WalletWasabi.Fluent.Models.UI;
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
	NavigationTarget = NavigationTarget.DialogScreen,
	Searchable = false)]
public partial class WalletSettingsViewModel : RoutableViewModel
{
	private readonly IWalletModel _wallet;
	[AutoNotify] private bool _preferPsbtWorkflow;
	[AutoNotify] private string _newWalletName;

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

		this.ValidateProperty(x => x.NewWalletName,
			errors =>
			{
				if (string.IsNullOrWhiteSpace(NewWalletName))
				{
					errors.Add(ErrorSeverity.Error, "The name cannot be empty");
				}
			});

		NewWalletName = _wallet.Name;

		CanRename = this.WhenAnyValue(x => x.NewWalletName, s => s != _wallet.Name);
		RenameCommand = ReactiveCommand.Create(OnRenameWallet, CanRename);
	}

	protected override void OnNavigatedFrom(bool isInHistory)
	{
		NewWalletName = _wallet.Name;
		base.OnNavigatedFrom(isInHistory);
	}

	public IObservable<bool> CanRename { get; }

	public ICommand RenameCommand { get; set; }

	public bool IsHardwareWallet { get; }

	public bool IsWatchOnly { get; }

	public ICommand VerifyRecoveryWordsCommand { get; }

	private void OnRenameWallet()
	{
		try
		{
			_wallet.Name = NewWalletName;
			this.RaisePropertyChanged(nameof(CanRename));
		}
		catch
		{
			UiContext.Navigate().To().ShowErrorDialog($"The wallet cannot be renamed to {NewWalletName}", "Invalid name", "Cannot rename the wallet", NavigationTarget.CompactDialogScreen);
			NewWalletName = _wallet.Name;
		}
	}
}
