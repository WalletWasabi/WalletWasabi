using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;
using System.Reactive.Disposables;
using System.Threading.Tasks;

namespace WalletWasabi.Fluent.ViewModels.AddWallet;

[NavigationMetaData(Title = "Success")]
public partial class AddedWalletPageViewModel : RoutableViewModel
{
	private readonly WalletSettingsModel _walletSettings;
	private IWalletModel? _wallet;

	public AddedWalletPageViewModel(UiContext uiContext, WalletSettingsModel walletSettings, WalletCreationOptions options) : base(uiContext)
	{
		_walletSettings = walletSettings;

		WalletName = options.WalletName!;
		WalletType = walletSettings.WalletType;

		SetupCancel(enableCancel: false, enableCancelOnEscape: false, enableCancelOnPressed: false);
		EnableBack = false;

		NextCommand = ReactiveCommand.CreateFromTask(() => OnNextAsync(options));
	}

	public WalletType WalletType { get; }

	public string WalletName { get; }

	private async Task OnNextAsync(WalletCreationOptions options)
	{
		if (_wallet is not { })
		{
			return;
		}

		// Block filters are only downloaded at startup, from the oldest wallet height. A wallet that needs older ones
		// (e.g. a hardware or an imported wallet) crashes the filter processor when it starts, so restart first (#14870).
		if (UiContext.Services.GetMinimumBlockHeight() is { } minHeight && _walletSettings.BestHeight + 1 < minHeight)
		{
			UiContext.Services.SetLastSelectedWallet(WalletName);
			UiContext.Services.UiConfig.ToFile();

			await ShowErrorAsync(
				"Restart required",
				"Wasabi needs to download older block filters for this wallet. The application will restart to begin this process.",
				"Add wallet");
			AppLifetimeHelper.Shutdown(withShutdownPrevention: true, restart: true);
			return;
		}

		IsBusy = true;

		await AutoLoginAsync(options);

		IsBusy = false;

		await Task.Delay(UiConstants.CloseSuccessDialogMillisecondsDelay);

		Navigate().Clear();

		UiContext.Navigate().To(_wallet);
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		_wallet = UiContext.WalletRepository.SaveWallet(_walletSettings);

		if (NextCommand is not null && NextCommand.CanExecute(default))
		{
			NextCommand.Execute(default);
		}
	}

	private async Task AutoLoginAsync(WalletCreationOptions? options)
	{
		if (_wallet is not { })
		{
			return;
		}

		var password =
			options switch
			{
				WalletCreationOptions.AddNewWallet add => add.SelectedWalletBackup?.Password,
				WalletCreationOptions.RecoverWallet rec => rec.WalletBackup?.Password,
				WalletCreationOptions.ConnectToHardwareWallet => "",
				_ => null
			};

		if (password is { })
		{
			await _wallet.Auth.LoginAsync(password);
		}
	}
}
