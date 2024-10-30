using System.IO;
using System.Reactive;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.ViewModels.AddWallet;

[NavigationMetaData(
	Order = 2,
	Category = SearchCategory.General,
	Title = "AddWalletPageViewModel_Title",
	Caption = "AddWalletPageViewModel_Caption",
	Keywords = "AddWalletPageViewModel_Keywords",
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
		var options = new WalletCreationOptions.AddNewWallet().WithNewMnemonic();
		Navigate().To().WalletNamePage(options);
	}

	private void OnConnectHardwareWallet()
	{
		Navigate().To().WalletNamePage(new WalletCreationOptions.ConnectToHardwareWallet());
	}

	private async Task OnImportWalletAsync()
	{
		try
		{
			var file = await FileDialogHelper.OpenFileAsync(Lang.Resources.AddWalletPageViewModel_OpenFiles_Title, new[] { "json" });

			if (file is null)
			{
				return;
			}

			var filePath = file.Path.LocalPath;
			var walletName = Path.GetFileNameWithoutExtension(filePath);

			var options = new WalletCreationOptions.ImportWallet(walletName, filePath);

			var validationError = UiContext.WalletRepository.ValidateWalletName(walletName);
			if (validationError is { })
			{
				Navigate().To().WalletNamePage(options);
				return;
			}

			var walletSettings = await UiContext.WalletRepository.NewWalletAsync(options);

			Navigate().To().AddedWalletPage(walletSettings, options);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			await ShowErrorAsync(
				Lang.Resources.AddWalletPageViewModel_Error_UnableImport_Title,
				ex.ToUserFriendlyString(),
				Lang.Resources.AddWalletPageViewModel_Error_UnableImport_Caption);
		}
	}

	private void OnRecoverWallet()
	{
		Navigate().To().WalletNamePage(new WalletCreationOptions.RecoverWallet());
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		var enableCancel = UiContext.WalletRepository.HasWallet;
		SetupCancel(enableCancel: enableCancel, enableCancelOnEscape: enableCancel, enableCancelOnPressed: enableCancel);
	}

	public async Task Activate()
	{
		MainViewModel.Instance.IsOobeBackgroundVisible = true;
		await NavigateDialogAsync(this, NavigationTarget.DialogScreen);
		MainViewModel.Instance.IsOobeBackgroundVisible = false;
	}
}
