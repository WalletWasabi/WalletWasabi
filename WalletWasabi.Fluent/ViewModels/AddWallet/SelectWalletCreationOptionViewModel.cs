using System.Reactive.Disposables;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.AddWallet.Create;
using WalletWasabi.Fluent.ViewModels.AddWallet.HardwareWallet;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.ViewModels.AddWallet;

[NavigationMetaData(Title = "Select an option")]
public partial class SelectWalletCreationOptionViewModel : RoutableViewModel
{
	public SelectWalletCreationOptionViewModel(string walletName)
	{
		EnableBack = true;

		RecoverWalletCommand = ReactiveCommand.Create(() => OnRecoverWallet(walletName));

		ImportWalletCommand = ReactiveCommand.CreateFromTask(async () => await OnImportWalletAsync(walletName));

		ConnectHardwareWalletCommand = ReactiveCommand.Create(() => OnConnectHardwareWallet(walletName));

		CreateWalletCommand = ReactiveCommand.CreateFromTask(async () => await OnCreateWalletAsync(walletName));
	}

	public ICommand CreateWalletCommand { get; }

	public ICommand RecoverWalletCommand { get; }

	public ICommand ImportWalletCommand { get; }

	public ICommand ConnectHardwareWalletCommand { get; }

	private void OnRecoverWallet(string walletName)
	{
		Navigate().To(new RecoverWalletViewModel(walletName));
	}

	private void OnConnectHardwareWallet(string walletName)
	{
		Navigate().To(new ConnectHardwareWalletViewModel(walletName));
	}

	private async Task OnImportWalletAsync(string walletName)
	{
		try
		{
			var filePath = await FileDialogHelper.ShowOpenFileDialogAsync("Import wallet file", new[] { "json" });

			if (filePath is null)
			{
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

	private async Task OnCreateWalletAsync(string walletName)
	{
		var dialogResult = await NavigateDialogAsync(
			new CreatePasswordDialogViewModel("Create Password", "Enter a password for your wallet.", enableEmpty: true)
			, NavigationTarget.CompactDialogScreen);

		if (dialogResult.Result is { } password)
		{
			IsBusy = true;

			var (km, mnemonic) = await Task.Run(
				() =>
				{
					var walletGenerator = new WalletGenerator(
						Services.WalletManager.WalletDirectories.WalletsDir,
						Services.WalletManager.Network)
					{
						TipHeight = Services.BitcoinStore.SmartHeaderChain.TipHeight
					};
					return walletGenerator.GenerateWallet(walletName, password);
				});

			Navigate().To(new RecoveryWordsViewModel(km, mnemonic));

			IsBusy = false;
		}
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		var enableCancel = Services.WalletManager.HasWallet();
		SetupCancel(enableCancel: enableCancel, enableCancelOnEscape: enableCancel, enableCancelOnPressed: false);
	}
}
