using System.IO;
using System.Reactive;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.ViewModels.AddWallet;

[NavigationMetaData(
	Title = "Add Wallet",
	Caption = "Create, recover or import wallet",
	Order = 2,
	Category = "General",
	Keywords = new[] { "Wallet", "Add", "Create", "Recover", "Import", "Connect", "Hardware", "ColdCard", "Trezor", "Ledger" },
	IconName = "add_circle_regular",
	NavigationTarget = NavigationTarget.DialogScreen,
	NavBarPosition = NavBarPosition.Bottom)]
public partial class AddWalletPageViewModel : DialogViewModelBase<Unit>
{
	public AddWalletPageViewModel()
	{
		CreateWalletCommand = ReactiveCommand.Create(OnCreateWallet);

		ConnectHardwareWalletCommand = ReactiveCommand.Create(OnConnectHardwareWallet);

		ImportWalletCommand = ReactiveCommand.CreateFromTask(async () => await OnImportWalletAsync());

		RecoverWalletCommand = ReactiveCommand.Create(OnRecoverWallet);

		OpenCommand = ReactiveCommand.Create(async () => await NavigateDialogAsync(this, NavigationTarget.DialogScreen));
	}

	public ICommand CreateWalletCommand { get; }

	public ICommand ConnectHardwareWalletCommand { get; }

	public ICommand ImportWalletCommand { get; }

	public ICommand RecoverWalletCommand { get; }

	private void OnCreateWallet()
	{
		Navigate().To(new WalletNamePageViewModel(WalletCreationOption.AddNewWallet));
	}

	private void OnConnectHardwareWallet()
	{
		Navigate().To(new WalletNamePageViewModel(WalletCreationOption.ConnectToHardwareWallet));
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
			if (validationError is { } err)
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
			await ShowErrorAsync(Title, ex.ToUserFriendlyString(),
				"The wallet file was not valid or compatible with Wasabi.");
		}
	}

	private void OnRecoverWallet()
	{
		Navigate().To(new WalletNamePageViewModel(WalletCreationOption.RecoverWallet));
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		var enableCancel = Services.WalletManager.HasWallet();
		SetupCancel(enableCancel: enableCancel, enableCancelOnEscape: enableCancel, enableCancelOnPressed: enableCancel);
	}
}
