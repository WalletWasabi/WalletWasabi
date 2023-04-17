using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Login;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public partial class WalletPageViewModel : ViewModelBase
{
	public IWalletModel WalletModel { get; }
	public Wallet Wallet { get; set; }

	public string Title => Wallet.WalletName;

	// TODO: Finish partial refactor
	// Wallet parameter must be removed
	private WalletPageViewModel(IWalletModel walletModel, Wallet wallet)
	{
		WalletModel = walletModel;
		Wallet = wallet;

		this.WhenAnyValue(x => x.IsSelected)
			.Where(x => !x && _disposable is { })
			.Do(_ => _disposable!.Dispose())
			.Subscribe();
	}

	[AutoNotify] private bool _isLoggedIn;
	[AutoNotify] private bool _isSelected;
	[AutoNotify] private RoutableViewModel? _currentPage;
	[AutoNotify] private WalletViewModel? _walletViewModel;

	private CompositeDisposable? _disposable;

	public void Activate()
	{
		_disposable?.Dispose();
		_disposable = new CompositeDisposable();

		this.WhenAnyValue(x => x.CurrentPage)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Skip(1)
			.Do(x => x?.Navigate().To(x))
			.Subscribe()
			.DisposeWith(_disposable);

		if (!IsLoggedIn && CurrentPage is not { })
		{
			CurrentPage = new LoginViewModel(UiContext, WalletModel);
		}
		else
		{
			CurrentPage?.Navigate().To(CurrentPage);
		}

		CurrentPage?.Navigate().To(CurrentPage, NavigationMode.Clear);
	}
}
