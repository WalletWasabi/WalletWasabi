using System.Reactive.Disposables;
using System.Threading.Tasks;
using WalletWasabi.Fluent.ViewModels.Login;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public class ClosedWalletViewModel : WalletViewModelBase
{
	protected ClosedWalletViewModel(Wallet wallet)
		: base(wallet)
	{
	}

	public LoadingViewModel? Loading { get; private set; }

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		Loading ??= new LoadingViewModel(Wallet);
		Loading.Activate(disposables);

		IsLoading = true;
	}

	protected override async Task OnOpen(NavigationMode defaultNavigationMode)
	{
		if (!Wallet.IsLoggedIn)
		{
			Navigate().To(new LoginViewModel(this), NavigationMode.Clear);
		}
		else
		{
			await base.OnOpen(defaultNavigationMode);
		}
	}

	public static WalletViewModelBase Create(Wallet wallet)
	{
		return wallet.KeyManager.IsHardwareWallet
			? new ClosedHardwareWalletViewModel(wallet)
			: wallet.KeyManager.IsWatchOnly
				? new ClosedWatchOnlyWalletViewModel(wallet)
				: new ClosedWalletViewModel(wallet);
	}
}
