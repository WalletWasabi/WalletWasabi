using System.IO;
using System.Reactive;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.ViewModels.AddWallet;

[NavigationMetaData(
	Title = "Add Wallet",
	Caption = "Create, connect, import or recover",
	Order = 2,
	Category = "General",
	Keywords = new[]
		{ "Wallet", "Add", "Create", "New", "Recover", "Import", "Connect", "Hardware", "ColdCard", "Trezor", "Ledger" },
	IconName = "nav_add_circle_24_regular",
	IconNameFocused = "nav_add_circle_24_filled",
	NavigationTarget = NavigationTarget.DialogScreen,
	NavBarPosition = NavBarPosition.Bottom,
	NavBarSelectionMode = NavBarSelectionMode.Button)]
public partial class AddWalletPageViewModel : DialogViewModelBase<Unit>
{
	private AddWalletPageViewModel()
	{
		CreateWalletCommand = ReactiveCommand.Create(OnCreateWallet);

		ConnectHardwareWalletCommand = ReactiveCommand.Create(OnConnectHardwareWallet);

		ImportWalletCommand = ReactiveCommand.CreateFromTask(OnImportWalletAsync);

		RecoverWalletCommand = ReactiveCommand.Create(OnRecoverWallet);
	}

	public ICommand CreateWalletCommand { get; }

	public ICommand ConnectHardwareWalletCommand { get; }

	public ICommand ImportWalletCommand { get; }

	public ICommand RecoverWalletCommand { get; }

	private void OnCreateWallet()
	{
		Navigate().To().WalletNamePage(WalletCreationOption.AddNewWallet);
	}

	private void OnConnectHardwareWallet()
	{
		Navigate().To().WalletNamePage(WalletCreationOption.ConnectToHardwareWallet);
	}

	private async Task OnImportWalletAsync()
	{
		try
		{
			var filePath = await FileDialogHelper.ShowOpenFileDialogAsync("Import wallet file", new[] { "json" });

			if (filePath is null)
			{
				return;
			}

			var walletName = Path.GetFileNameWithoutExtension(filePath);

			var validationError = WalletHelpers.ValidateWalletName(walletName);
			if (validationError is { })
			{
				Navigate().To().WalletNamePage(WalletCreationOption.ImportWallet, filePath);
				return;
			}

			var keyManager = await ImportWalletHelper.ImportWalletAsync(Services.WalletManager, walletName, filePath);

			// TODO: Remove this after current ViewModel is decoupled
			var walletSettings = new WalletSettingsModel(keyManager, true);

			Navigate().To().AddedWalletPage(walletSettings);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			await ShowErrorAsync("Import wallet", ex.ToUserFriendlyString(), "Wasabi was unable to import your wallet.");
		}
	}

	private void OnRecoverWallet()
	{
		Navigate().To().WalletNamePage(WalletCreationOption.RecoverWallet);
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		var enableCancel = Services.WalletManager.HasWallet();
		SetupCancel(enableCancel: enableCancel, enableCancelOnEscape: enableCancel, enableCancelOnPressed: enableCancel);
	}

	public async Task Activate()
	{
		MainViewModel.Instance.IsOobeBackgroundVisible = true;
		await NavigateDialogAsync(this, NavigationTarget.DialogScreen);
		MainViewModel.Instance.IsOobeBackgroundVisible = false;
	}
}
