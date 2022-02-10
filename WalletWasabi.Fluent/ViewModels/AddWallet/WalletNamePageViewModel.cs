using ReactiveUI;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Models;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using System.Threading.Tasks;
using WalletWasabi.Fluent.ViewModels.AddWallet.Create;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.AddWallet.HardwareWallet;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.AddWallet;

[NavigationMetaData(Title = "Enter wallet name")]
public partial class WalletNamePageViewModel : RoutableViewModel
{
	[AutoNotify] private string _walletName = "";

	public WalletNamePageViewModel(WalletCreationOption creationOption)
	{
		EnableBack = true;

		var canExecute = this.WhenAnyValue(x => x.WalletName)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Select(x => !string.IsNullOrWhiteSpace(x) && !Validations.Any);

		NextCommand = ReactiveCommand.CreateFromTask(async () => await OnNextAsync(WalletName, creationOption), canExecute);

		this.ValidateProperty(x => x.WalletName, errors => ValidateWalletName(errors, WalletName));
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
			default:
				throw new InvalidOperationException($"WalletCreationOption not supported: {creationOption}");
		}
	}

	private async Task CreatePasswordAsync(string walletName)
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

	private static void ValidateWalletName(IValidationErrors errors, string walletName)
	{
		string walletFilePath = Path.Combine(Services.WalletManager.WalletDirectories.WalletsDir, $"{walletName}.json");

		if (string.IsNullOrEmpty(walletName))
		{
			return;
		}

		if (walletName.IsTrimmable())
		{
			errors.Add(ErrorSeverity.Error, "Leading and trailing white spaces are not allowed!");
			return;
		}

		if (File.Exists(walletFilePath))
		{
			errors.Add(
				ErrorSeverity.Error,
				$"A wallet named {walletName} already exists. Please try a different name.");
			return;
		}

		if (!WalletGenerator.ValidateWalletName(walletName))
		{
			errors.Add(ErrorSeverity.Error, "Selected Wallet is not valid. Please try a different name.");
		}
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		var enableCancel = Services.WalletManager.HasWallet();
		SetupCancel(enableCancel: enableCancel, enableCancelOnEscape: enableCancel, enableCancelOnPressed: enableCancel);
	}
}
