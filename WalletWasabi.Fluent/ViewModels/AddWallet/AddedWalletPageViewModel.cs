using ReactiveUI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;
using System.Reactive.Disposables;
using WalletWasabi.Fluent.Models;
using System.Threading.Tasks;

namespace WalletWasabi.Fluent.ViewModels.AddWallet;

[NavigationMetaData(Title = "Success")]
public partial class AddedWalletPageViewModel : RoutableViewModel
{
	private readonly WalletCreationOptions? _options;
	private readonly IWalletSettingsModel _walletSettings;
	private IWalletModel? _wallet;

	private AddedWalletPageViewModel(IWalletSettingsModel walletSettings, WalletCreationOptions? options)
	{
		_walletSettings = walletSettings;
		_options = options;

		WalletName = walletSettings.WalletName;
		WalletType = walletSettings.WalletType;

		SetupCancel(enableCancel: false, enableCancelOnEscape: false, enableCancelOnPressed: false);
		EnableBack = false;

		NextCommand = ReactiveCommand.CreateFromTask(OnNextAsync);
	}

	public WalletType WalletType { get; }

	public string WalletName { get; }

	private async Task OnNextAsync()
	{
		await AutoLoginAsync();

		Navigate().Clear();
		UiContext.Navigate().To(_wallet!);
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		_wallet = UiContext.WalletRepository.SaveWallet(_walletSettings);
	}

	private async Task AutoLoginAsync()
	{
		var password =
			_options switch
			{
				WalletCreationOptions.AddNewWallet add => add.Password,
				WalletCreationOptions.RecoverWallet rec => rec.Password,
				_ => null
			};

		if (password is { })
		{
			var (result, _) = await TermsAndConditionsViewModel.TryShowAsync(UiContext, _wallet!);
			if (result)
			{
				await _wallet!.Auth.LoginAsync(password);
			}
		}
	}
}
