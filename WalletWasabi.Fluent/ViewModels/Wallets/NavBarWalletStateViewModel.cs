using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Login;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public partial class NavBarWalletStateViewModel : ViewModelBase
{
	public Wallet Wallet { get; }

	public string Title => Wallet.WalletName;

	public NavBarWalletStateViewModel(Wallet wallet)
	{
		Wallet = wallet;
	}

	[AutoNotify] private bool _isLoggedIn;
	[AutoNotify] private bool _isSelected;
	[AutoNotify] private RoutableViewModel? _currentPage;
	[AutoNotify] private WalletViewModel? _walletViewModel;

	private CompositeDisposable? _viewDisposable;

	public void Activate()
	{
		_viewDisposable?.Dispose();
		_viewDisposable = new CompositeDisposable();

		this.WhenAnyValue(x => x.CurrentPage)
			.Do(x=>x?.Navigate().To(x))
			.Subscribe()
			.DisposeWith(_viewDisposable);

		if (!IsLoggedIn && CurrentPage is not { })
		{
			CurrentPage = new LoginViewModel(this);
		}
		else
		{
			CurrentPage?.Navigate().To(CurrentPage);
		}
	}
}
