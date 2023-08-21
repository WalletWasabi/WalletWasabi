using ReactiveUI;
using System.Linq;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;
using System.Reactive.Disposables;
using System.Threading.Tasks;

namespace WalletWasabi.Fluent.ViewModels.AddWallet;

[NavigationMetaData(Title = "Success")]
public partial class AddedWalletPageViewModel : RoutableViewModel
{
	private readonly IWalletSettingsModel _walletSettings;
	private IWalletModel _wallet;

	private AddedWalletPageViewModel(IWalletSettingsModel walletSettings)
	{
		_walletSettings = walletSettings;

		WalletName = walletSettings.WalletName;
		WalletType = walletSettings.WalletType;

		SetupCancel(enableCancel: false, enableCancelOnEscape: false, enableCancelOnPressed: false);
		EnableBack = false;

		NextCommand = ReactiveCommand.CreateFromTask(async () => await OnNextAsync(_wallet));
	}

	public WalletType WalletType { get; }

	public string WalletName { get; }

	private async Task OnNextAsync(IWalletModel walletModel)
	{
		Navigate().Clear();
		await TryToLoginAsync(walletModel);
		UiContext.Navigate().To(walletModel);
	}

	private async Task TryToLoginAsync(IWalletModel walletModel)
	{
		if (_wallet.Auth.IsLegalRequired)
		{
			var accepted = await ShowLegalAsync();
			if (accepted)
			{
				await walletModel.Auth.AcceptTermsAndConditions();
				walletModel.Auth.CompleteLogin();
			}
			else
			{
				walletModel.Auth.Logout();
				// TODO: ErrorMessage = "You must accept the Terms and Conditions!";
			}
		}
		else
		{
			walletModel.Auth.CompleteLogin();
		}
	}

	private async Task<bool> ShowLegalAsync()
	{
		return await Navigate().To().TermsAndConditions().GetResultAsync();
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		_wallet = UiContext.WalletRepository.SaveWallet(_walletSettings);
	}
}
