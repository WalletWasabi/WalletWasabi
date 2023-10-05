using System.Linq;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Login;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.SearchBar.SearchItems;
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

		SetIcon();

		SearchItems = CreateSearchItems();

		this.WhenAnyValue(x => x.IsSelected, x => x.IsLoggedIn, (selected, loggedIn) => selected && loggedIn)
			.Do(AddOrRemoveSearchItems)
			.Subscribe();
	}

	public IWalletModel WalletModel { get; }

	public Wallet Wallet { get; }

	public string Title => WalletModel.Name;

	private ISearchItem[] SearchItems { get; }

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
		WalletViewModel = WalletViewModel.Create(UiContext, this);
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

	private ISearchItem[] CreateSearchItems()
	{
		return new ISearchItem[]
		{
			new ActionableItem("Send", "Display wallet send dialog", async () => UiContext.Navigate().To().Send(WalletViewModel!), "Wallet", new[] { "Wallet", "Send", "Action", }) { Icon = "wallet_action_send", IsDefault = true },
			new ActionableItem("Receive", "Display wallet receive dialog", async () => UiContext.Navigate().To().Receive(WalletModel), "Wallet", new[] { "Wallet", "Receive", "Action", }) { Icon = "wallet_action_receive", IsDefault = true },
			new ActionableItem("Coinjoin Settings", "Display wallet coinjoin settings", async () => UiContext.Navigate().To().CoinJoinSettings(WalletModel), "Wallet", new[] { "Wallet", "Settings", }) { Icon = "wallet_action_coinjoin", IsDefault = true },
			new ActionableItem("Wallet Settings", "Display wallet settings", async () => UiContext.Navigate().To().WalletSettings(WalletModel), "Wallet", new[] { "Wallet", "Settings", }) { Icon = "settings_wallet_regular", IsDefault = true },
			new ActionableItem("Wallet Coins", "Display wallet coins", async () => UiContext.Navigate().To().WalletCoins(WalletModel), "Wallet", new[] { "Wallet", "Coins", "UTXO", }) { Icon = "wallet_coins", IsDefault = true },
			new ActionableItem("Wallet Stats", "Display wallet stats", async () => UiContext.Navigate().To().WalletStats(WalletViewModel!), "Wallet", new[] { "Wallet", "Stats", }) { Icon = "stats_wallet_regular", IsDefault = true },
			new ActionableItem("Wallet Info", "Display wallet info", async () => UiContext.Navigate().To().WalletInfo(WalletModel), "Wallet", new[] { "Wallet", "Info", }) { Icon = "info_regular", IsDefault = true },
		};
	}

	private void AddOrRemoveSearchItems(bool shouldAdd)
	{
		if (shouldAdd)
		{
			UiContext.EditableSearchSource.Add(SearchItems);
		}
		else
		{
			UiContext.EditableSearchSource.Remove(SearchItems);
		}
	}
}
