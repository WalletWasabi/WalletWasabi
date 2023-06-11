using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Validation;
using System.Threading.Tasks;
using WalletWasabi.Fluent.ViewModels.AddWallet.Create;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.AddWallet.HardwareWallet;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Helpers;
using NBitcoin;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.ViewModels.AddWallet;

[NavigationMetaData(Title = "Wallet Name")]
public partial class WalletNamePageViewModel : RoutableViewModel
{
	[AutoNotify] private string _walletName;
	private readonly string? _importFilePath;
	private readonly Lazy<Mnemonic> _mnemonic = new(() => new Mnemonic(Wordlist.English, WordCount.Twelve));

	private WalletNamePageViewModel(WalletCreationOption creationOption, string? importFilePath = null)
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
		IsBusy = true;

		// Makes sure we can create a wallet with this wallet name.
		await Task.Run(() => WalletGenerator.GetWalletFilePath(walletName, Services.WalletManager.WalletDirectories.WalletsDir));

		IsBusy = false;

		switch (creationOption)
		{
			case WalletCreationOption.AddNewWallet:
				Navigate().To().RecoveryWords(_mnemonic.Value, walletName);
				break;

			case WalletCreationOption.ConnectToHardwareWallet:
				Navigate().To().ConnectHardwareWallet(walletName);
				break;

			case WalletCreationOption.RecoverWallet:
				Navigate().To().RecoverWallet(walletName);
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
			Navigate().To().AddedWalletPage(keyManager);
		}
		catch (Exception ex)
		{
			await ShowErrorAsync("Import wallet", ex.ToUserFriendlyString(), "Wasabi was unable to import your wallet.");
			BackCommand.Execute(null);
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
