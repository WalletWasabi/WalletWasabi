using System.Reactive.Disposables;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Login;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public class ClosedWalletViewModel : WalletViewModelBase
{
	private CompositeDisposable? _disposable;

	protected ClosedWalletViewModel(Wallet wallet)
		: base(wallet)
	{
		Loading = new LoadingViewModel(Wallet);

		this.WhenAnyValue(x => x.Loading.IsLoading)
			.BindTo(this, x => x.IsLoading);
	}

	public LoadingViewModel Loading { get; }

	public void StartLoading()
	{
		_disposable?.Dispose();
		_disposable = new CompositeDisposable();
		Loading.Activate(_disposable);
	}

	public void StopLoading()
	{
		_disposable?.Dispose();
		_disposable = null;
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
