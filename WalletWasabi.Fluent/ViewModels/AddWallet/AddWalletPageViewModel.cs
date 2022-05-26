using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.ViewModels.AddWallet;

[NavigationMetaData(
	Title = "Add Wallet",
	Caption = "Create, recover or import wallet",
	Order = 2,
	Category = "General",
	Keywords = new[]
		{"Wallet", "Add", "Create", "New", "Recover", "Import", "Connect", "Hardware", "ColdCard", "Trezor", "Ledger"},
	IconName = "nav_add_circle_24_regular",
	IconNameFocused = "nav_add_circle_24_filled",
	NavigationTarget = NavigationTarget.DialogScreen,
	NavBarPosition = NavBarPosition.Bottom)]
public partial class AddWalletPageViewModel : DialogViewModelBase<Unit>
{
	[AutoNotify] private AddWalletPageOption _selectedOption;

	public AddWalletPageViewModel()
	{
		SelectionMode = NavBarItemSelectionMode.Button;

		Options = new()
		{
			new AddWalletPageOption
			{
				CreationOption = WalletCreationOption.AddNewWallet,
				Title = "Create a new wallet",
				IconName = "add_regular"
			},
			new AddWalletPageOption
			{
				CreationOption = WalletCreationOption.ConnectToHardwareWallet,
				Title = "Connect to hardware wallet",
				IconName = "calculator_regular"
			},
			new AddWalletPageOption
			{
				CreationOption = WalletCreationOption.ImportWallet,
				Title = "Import a wallet",
				IconName = "import_regular"
			},
			new AddWalletPageOption
			{
				CreationOption = WalletCreationOption.RecoverWallet,
				Title = "Recover a wallet",
				IconName = "recover_arrow_right_regular"
			},
		};

		_selectedOption = Options.First();

		OpenCommand = ReactiveCommand.Create(async () =>
		{
			MainViewModel.Instance.IsOobeBackgroundVisible = true;
			await NavigateDialogAsync(this, NavigationTarget.DialogScreen);
			MainViewModel.Instance.IsOobeBackgroundVisible = false;
		});

		NextCommand = ReactiveCommand.CreateFromTask(OnNextAsync);
	}

	public List<AddWalletPageOption> Options { get; }

	private async Task ImportWalletAsync()
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
				Navigate().To(new WalletNamePageViewModel(WalletCreationOption.ImportWallet, filePath));
				return;
			}

			var keyManager = await ImportWalletHelper.ImportWalletAsync(Services.WalletManager, walletName, filePath);
			Navigate().To(new AddedWalletPageViewModel(keyManager));
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			await ShowErrorAsync("Import wallet", ex.ToUserFriendlyString(), "Wasabi was unable to import your wallet.");
		}
	}

	private async Task OnNextAsync()
	{
		if (SelectedOption.CreationOption == WalletCreationOption.ImportWallet)
		{
			await ImportWalletAsync();
		}
		else
		{
			Navigate().To(new WalletNamePageViewModel(SelectedOption.CreationOption));
		}
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		var enableCancel = Services.WalletManager.HasWallet();
		SetupCancel(enableCancel: enableCancel, enableCancelOnEscape: enableCancel, enableCancelOnPressed: enableCancel);

		SelectedOption = Options.First();
	}
}
