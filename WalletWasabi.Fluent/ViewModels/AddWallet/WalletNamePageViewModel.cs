using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Validation;
using System.Threading.Tasks;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Models;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.UI;

namespace WalletWasabi.Fluent.ViewModels.AddWallet;

[NavigationMetaData(Title = "Wallet Name")]
public partial class WalletNamePageViewModel : RoutableViewModel
{
	[AutoNotify] private string _walletName;
	private readonly WalletCreationOptions _options;

	public WalletNamePageViewModel(UiContext uiContext, WalletCreationOptions options)
	{
		UiContext = uiContext;

		_options = options;
		_walletName = UiContext.WalletRepository.GetNextWalletName();

		EnableBack = true;

		var nextCommandCanExecute =
			this.WhenAnyValue(x => x.WalletName)
				.Select(x => !string.IsNullOrWhiteSpace(x) && !Validations.Any);

		NextCommand = ReactiveCommand.CreateFromTask(OnNextAsync, nextCommandCanExecute);

		this.ValidateProperty(x => x.WalletName, ValidateWalletName);
	}

	private async Task OnNextAsync()
	{
		IsBusy = true;

		// Makes sure we can create a wallet with this wallet name.
		await Task.Run(() => WalletGenerator.GetWalletFilePath(WalletName, Services.WalletManager.WalletDirectories.WalletsDir));

		IsBusy = false;

		switch (_options)
		{
			case WalletCreationOptions.AddNewWallet add:
				Navigate().To().RecoveryWords(add with { WalletName = WalletName });
				break;

			case WalletCreationOptions.ConnectToHardwareWallet chw:
				Navigate().To().ConnectHardwareWallet(chw with { WalletName = WalletName });
				break;

			case WalletCreationOptions.RecoverWallet rec:
				Navigate().To().RecoverWallet(rec with { WalletName = WalletName });
				break;

			case WalletCreationOptions.ImportWallet imp:
				await ImportWalletAsync(imp with { WalletName = WalletName });
				break;

			default:
				throw new InvalidOperationException($"{nameof(WalletCreationOptions)} not supported: {_options?.GetType().Name}");
		}
	}

	private async Task ImportWalletAsync(WalletCreationOptions.ImportWallet options)
	{
		try
		{
			var walletSettings = await UiContext.WalletRepository.NewWalletAsync(options);
			Navigate().To().AddedWalletPage(walletSettings);
		}
		catch (Exception ex)
		{
			await ShowErrorAsync("Import wallet", ex.ToUserFriendlyString(), "Wasabi was unable to import your wallet.");
			BackCommand.Execute(null);
		}
	}

	private void ValidateWalletName(IValidationErrors errors)
	{
		var error = UiContext.WalletRepository.ValidateWalletName(WalletName);
		if (error is { } e)
		{
			errors.Add(e.Severity, e.Message);
		}
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		var enableCancel = UiContext.WalletRepository.HasWallet;
		SetupCancel(enableCancel: enableCancel, enableCancelOnEscape: enableCancel, enableCancelOnPressed: enableCancel);
	}
}
