using System.Linq;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Login;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public partial class WalletPageViewModel : ViewModelBase
{
	[AutoNotify] private bool _isLoggedIn;
	[AutoNotify] private bool _isSelected;
	[AutoNotify] private bool _isLoading;
	[AutoNotify] private string? _iconName;
	[AutoNotify] private string? _iconNameFocused;
	[AutoNotify] private WalletViewModel? _walletViewModel;
	[AutoNotify] private RoutableViewModel? _currentPage;
	[AutoNotify] private string? _title;

	private WalletPageViewModel(IWalletModel walletModel)
	{
		WalletModel = walletModel;

		// TODO: Finish partial refactor
		// Wallet property must be removed
		Wallet = Services.WalletManager.GetWallets(false).First(x => x.WalletName == walletModel.Name);

		// Show Login Page when wallet is not logged in
		this.WhenAnyValue(x => x.IsLoggedIn)
			.Where(x => !x)
			.Do(_ => ShowLogin())
			.Subscribe();

		// Show Loading page when wallet is logged in
		this.WhenAnyValue(x => x.IsLoggedIn)
			.Where(x => x)
			.Do(_ => ShowWalletLoading())
			.Subscribe();

		// Show main Wallet UI when wallet load is completed
		this.WhenAnyObservable(x => x.WalletModel.Loader.LoadCompleted)
			.Do(_ => ShowWallet())
			.Subscribe();

		this.WhenAnyValue(x => x.WalletModel.Auth.IsLoggedIn)
			.BindTo(this, x => x.IsLoggedIn);

		// Navigate to current page when IsSelected and CurrentPage change
		this.WhenAnyValue(x => x.IsSelected, x => x.CurrentPage)
			.Where(t => t.Item1)
			.Select(t => t.Item2)
			.WhereNotNull()
			.Do(x => UiContext.Navigate().To(x, NavigationTarget.HomeScreen, NavigationMode.Clear))
			.Subscribe();

        this.WhenAnyValue(x => x.WalletModel.Name).BindTo(this, x => x.Title);

		SetIcon();
	}

	public IWalletModel WalletModel { get; }

	public Wallet Wallet { get; }

	private void ShowLogin()
	{
		CurrentPage = new LoginViewModel(UiContext, WalletModel);
	}

	private void ShowWalletLoading()
	{
		CurrentPage = new LoadingViewModel(WalletModel);
		IsLoading = true;
	}

	private void ShowWallet()
	{
		WalletViewModel =
			WalletModel.IsHardwareWallet
			? new HardwareWalletViewModel(UiContext, WalletModel, Wallet)
			: new WalletViewModel(UiContext, WalletModel, Wallet);

		// Pass IsSelected down to WalletViewModel.IsSelected
		this.WhenAnyValue(x => x.IsSelected)
			.BindTo(WalletViewModel, x => x.IsSelected);

		CurrentPage = WalletViewModel;
		IsLoading = false;
	}

	private void SetIcon()
	{
		var walletType = WalletModel.Settings.WalletType;

		var baseResourceName = walletType switch
		{
			WalletType.Coldcard => "coldcard_24",
			WalletType.Trezor => "trezor_24",
			WalletType.Ledger => "ledger_24",
			_ => "wallet_24"
		};

		IconName = $"nav_{baseResourceName}_regular";
		IconNameFocused = $"nav_{baseResourceName}_filled";
	}
}
