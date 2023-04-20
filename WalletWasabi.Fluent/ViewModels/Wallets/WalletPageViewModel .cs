using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Login;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public partial class WalletPageViewModel : StandaloneActivatableViewModel
{
	[AutoNotify] private bool _isLoggedIn;
	[AutoNotify] private bool _isSelected;
	[AutoNotify] private WalletViewModel? _walletViewModel;
	[AutoNotify] private RoutableViewModel? _currentPage;

	private WalletPageViewModel(IWalletModel walletModel)
	{
		WalletModel = walletModel;

		// TODO: Finish partial refactor
		// Wallet property must be removed
		Wallet = Services.WalletManager.GetWallets(false).First(x => x.WalletName == walletModel.Name);
	}

	public IWalletModel WalletModel { get; }
	public Wallet Wallet { get; set; }

	public string Title => WalletModel.Name;

	protected override void OnActivated(CompositeDisposable disposables)
	{
		IsSelected = true;

		this.WhenAnyValue(x => x.CurrentPage)
			.WhereNotNull()
			.Do(x => UiContext.Navigate().To(x, NavigationTarget.HomeScreen, NavigationMode.Clear))
			.Subscribe()
			.DisposeWith(disposables);

		this.WhenAnyValue(x => x.IsLoggedIn)
			.Do(isLoggedIn =>
			{
				if (!isLoggedIn && CurrentPage is not { })
				{
					CurrentPage = new LoginViewModel(UiContext, WalletModel, Wallet);
				}
				else if (isLoggedIn && CurrentPage is LoginViewModel)
				{
					CurrentPage = new LoadingViewModel(Wallet);
				}
			})
			.Subscribe()
			.DisposeWith(disposables);

		WalletModel.WhenAnyValue(x => x.IsLoggedIn)
				   .BindTo(this, x => x.IsLoggedIn)
				   .DisposeWith(disposables);

		WalletModel.State
				   .Where(x => x == WalletState.Started)
				   .Do(_ => ShowWallet())
				   .Subscribe()
				   .DisposeWith(disposables);
	}

	protected override void OnDeactivated()
	{
		IsSelected = false;
	}

	private void ShowWallet()
	{
		WalletViewModel = WalletViewModel.Create(UiContext, this);
		CurrentPage = WalletViewModel;
	}
}
