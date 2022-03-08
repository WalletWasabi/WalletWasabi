using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using System.Threading.Tasks;
using WalletWasabi.Fluent.ViewModels.AddWallet.Create;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.AddWallet.HardwareWallet;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.ViewModels.AddWallet;

[NavigationMetaData(Title = "Wallet Name")]
public partial class WalletNamePageViewModel : RoutableViewModel
{
	[AutoNotify] private string _walletName = "";
	private readonly string? _importFilePath;

	public WalletNamePageViewModel(WalletCreationOption creationOption, string? importFilePath = null)
	{
		_importFilePath = importFilePath;

		_walletName = Services.WalletManager.WalletDirectories.GetNextWalletName("Wallet");

		EnableBack = true;

		var canExecute =
			this.WhenAnyValue(x => x.WalletName)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Select(x => !string.IsNullOrWhiteSpace(x) && !Validations.Any);

		NextCommand = ReactiveCommand.CreateFromTask(async () => await OnNextAsync(WalletName, creationOption), canExecute);

		this.ValidateProperty(x => x.WalletName, ValidateWalletName);
	}

	private async Task OnNextAsync(string walletName, WalletCreationOption creationOption)
	{
		switch (creationOption)
		{
			case WalletCreationOption.AddNewWallet:
				await CreatePasswordAsync(walletName);
				break;

			case WalletCreationOption.ConnectToHardwareWallet:
				Navigate().To(new ConnectHardwareWalletViewModel(walletName));
				break;

			case WalletCreationOption.RecoverWallet:
				Navigate().To(new RecoverWalletViewModel(walletName));
				break;

			case WalletCreationOption.ImportWallet when _importFilePath is { }:
				await ImportWalletAsync(walletName, _importFilePath);
				break;

			default:
				throw new InvalidOperationException($"{nameof(WalletCreationOption)} not supported: {creationOption}");
		}
	}

	private async Task ImportWalletAsync(string walletName, string filePath)
	{
		try
		{
			var keyManager = await ImportWalletHelper.ImportWalletAsync(Services.WalletManager, walletName, filePath);
			Navigate().To(new AddedWalletPageViewModel(keyManager));
		}
		catch (Exception ex)
		{
			await ShowErrorAsync("Import wallet", ex.ToUserFriendlyString(), "Wasabi was unable to import your wallet.");
			BackCommand.Execute(null);
		}
	}

	private async Task CreatePasswordAsync(string walletName)
	{
		var dialogResult = await NavigateDialogAsync(
			new CreatePasswordDialogViewModel("Add Password", enableEmpty: true),
			NavigationTarget.CompactDialogScreen);

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

	private void ValidateWalletName(IValidationErrors errors)
	{
		var error = WalletHelpers.ValidateWalletName(WalletName);
		if (error is { } e)
		{
			errors.Add(e.Severity, e.Message);
		}
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		var enableCancel = Services.WalletManager.HasWallet();
		SetupCancel(enableCancel: enableCancel, enableCancelOnEscape: enableCancel, enableCancelOnPressed: enableCancel);
	}
}
