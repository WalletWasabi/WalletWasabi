using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Avalonia.Controls;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.AddWallet;
using WalletWasabi.Fluent.ViewModels.Dialogs.Authorization;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.HelpAndSupport;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.OpenDirectory;
using WalletWasabi.Fluent.ViewModels.SearchBar;
using WalletWasabi.Fluent.ViewModels.SearchBar.Sources;
using WalletWasabi.Fluent.ViewModels.Settings;
using WalletWasabi.Fluent.ViewModels.StatusIcon;
using WalletWasabi.Fluent.ViewModels.TransactionBroadcasting;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Advanced;
using WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;
using WalletWasabi.Fluent.ViewModels.Wallets.Receive;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;

namespace WalletWasabi.Fluent.ViewModels;

public partial class MainViewModel : ViewModelBase
{
	private readonly SettingsPageViewModel _settingsPage;
	private readonly PrivacyModeViewModel _privacyMode;
	private readonly AddWalletPageViewModel _addWalletPage;
	[AutoNotify] private DialogScreenViewModel _dialogScreen;
	[AutoNotify] private DialogScreenViewModel _fullScreen;
	[AutoNotify] private DialogScreenViewModel _compactDialogScreen;
	[AutoNotify] private NavBarViewModel _navBar;
	[AutoNotify] private StatusIconViewModel _statusIcon;
	[AutoNotify] private string _title = "Wasabi Wallet";
	[AutoNotify] private WindowState _windowState;
	[AutoNotify] private bool _isOobeBackgroundVisible;
	[AutoNotify] private bool _isCoinJoinActive;

	public MainViewModel(UiContext uiContext)
	{
		UiContext = uiContext;
		ApplyUiConfigWindowState();

		_dialogScreen = new DialogScreenViewModel();
		_fullScreen = new DialogScreenViewModel(NavigationTarget.FullScreen);
		_compactDialogScreen = new DialogScreenViewModel(NavigationTarget.CompactDialogScreen);
		_navBar = new NavBarViewModel(UiContext);
		MainScreen = new TargettedNavigationStack(NavigationTarget.HomeScreen);
		UiContext.RegisterNavigation(new NavigationState(UiContext, MainScreen, DialogScreen, FullScreen, CompactDialogScreen, _navBar));

		_navBar.Activate();

		UiServices.Initialize(UiContext);

		_statusIcon = new StatusIconViewModel(new TorStatusCheckerModel(Services.TorStatusChecker));

		_addWalletPage = new AddWalletPageViewModel(UiContext);
		_settingsPage = new SettingsPageViewModel(UiContext);
		_privacyMode = new PrivacyModeViewModel();

		NavigationManager.RegisterType(_navBar);
		RegisterViewModels();

		RxApp.MainThreadScheduler.Schedule(async () => await _navBar.InitialiseAsync());

		this.WhenAnyValue(x => x.WindowState)
			.Where(state => state != WindowState.Minimized)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(state => Services.UiConfig.WindowState = state.ToString());

		IsMainContentEnabled = this.WhenAnyValue(
				x => x.DialogScreen.IsDialogOpen,
				x => x.FullScreen.IsDialogOpen,
				x => x.CompactDialogScreen.IsDialogOpen,
				(dialogIsOpen, fullScreenIsOpen, compactIsOpen) => !(dialogIsOpen || fullScreenIsOpen || compactIsOpen))
			.ObserveOn(RxApp.MainThreadScheduler);

		CurrentWallet = this.WhenAnyValue(x => x.MainScreen.CurrentPage)
			.WhereNotNull()
			.OfType<WalletViewModel>();

		IsOobeBackgroundVisible = Services.UiConfig.Oobe;

		RxApp.MainThreadScheduler.Schedule(async () =>
		{
			if (!Services.WalletManager.HasWallet() || Services.UiConfig.Oobe)
			{
				IsOobeBackgroundVisible = true;

				await UiContext.Navigate().To().WelcomePage(_addWalletPage).GetResultAsync();

				if (Services.WalletManager.HasWallet())
				{
					Services.UiConfig.Oobe = false;
					IsOobeBackgroundVisible = false;
				}
			}
		});

		SearchBar = CreateSearchBar();

		NetworkBadgeName = Services.PersistentConfig.Network == Network.Main ? "" : Services.PersistentConfig.Network.Name;

		// TODO: the reason why this MainViewModel singleton is even needed thoughout the codebase is dubious.
		// Also it causes tight coupling which damages testability.
		// We should strive to remove it altogether.
		if (Instance != null)
		{
			throw new InvalidOperationException($"MainViewModel instantiated more than once.");
		}

		Instance = this;
	}

	public IObservable<bool> IsMainContentEnabled { get; }

	public string NetworkBadgeName { get; }

	public IObservable<WalletViewModel> CurrentWallet { get; }

	public TargettedNavigationStack MainScreen { get; }

	public SearchBarViewModel SearchBar { get; }

	public static MainViewModel Instance { get; private set; }

	public bool IsBusy =>
		MainScreen.CurrentPage is { IsBusy: true } ||
		DialogScreen.CurrentPage is { IsBusy: true } ||
		FullScreen.CurrentPage is { IsBusy: true } ||
		CompactDialogScreen.CurrentPage is { IsBusy: true };

	public bool IsDialogOpen()
	{
		return DialogScreen.IsDialogOpen
			   || FullScreen.IsDialogOpen
			   || CompactDialogScreen.IsDialogOpen;
	}

	public void ShowDialogAlert()
	{
		if (CompactDialogScreen.IsDialogOpen)
		{
			CompactDialogScreen.ShowAlert = false;
			CompactDialogScreen.ShowAlert = true;
			return;
		}

		if (DialogScreen.IsDialogOpen)
		{
			DialogScreen.ShowAlert = false;
			DialogScreen.ShowAlert = true;
			return;
		}

		if (FullScreen.IsDialogOpen)
		{
			FullScreen.ShowAlert = false;
			FullScreen.ShowAlert = true;
		}
	}

	public void ClearStacks()
	{
		MainScreen.Clear();
		DialogScreen.Clear();
		FullScreen.Clear();
		CompactDialogScreen.Clear();
	}

	public void InvalidateIsCoinJoinActive()
	{
		// TODO: Workaround for deprecation of WalletManagerViewModel
		// REMOVE after IWalletModel.IsCoinjoining is implemented
		IsCoinJoinActive =
			NavBar.Wallets
				  .Select(x => x.WalletViewModel)
				  .WhereNotNull()
				  .Any(x => x.IsCoinJoining);
	}

	public void Initialize()
	{
		StatusIcon.Initialize();

		if (Services.PersistentConfig.Network != Network.Main)
		{
			Title += $" - {Services.PersistentConfig.Network}";
		}
	}

	private void RegisterViewModels()
	{
		PrivacyModeViewModel.Register(_privacyMode);
		AddWalletPageViewModel.Register(_addWalletPage);
		SettingsPageViewModel.Register(_settingsPage);

		GeneralSettingsTabViewModel.RegisterLazy(() =>
		{
			_settingsPage.SelectedTab = 0;
			return _settingsPage;
		});

		BitcoinTabSettingsViewModel.RegisterLazy(() =>
		{
			_settingsPage.SelectedTab = 1;
			return _settingsPage;
		});

		AdvancedSettingsTabViewModel.RegisterLazy(() =>
		{
			_settingsPage.SelectedTab = 2;
			return _settingsPage;
		});

		AboutViewModel.RegisterLazy(() => new AboutViewModel(UiContext));
		BroadcasterViewModel.RegisterLazy(() => new BroadcasterViewModel(UiContext));
		LegalDocumentsViewModel.RegisterLazy(() => new LegalDocumentsViewModel(UiContext));
		UserSupportViewModel.RegisterLazy(() => new UserSupportViewModel());
		BugReportLinkViewModel.RegisterLazy(() => new BugReportLinkViewModel());
		DocsLinkViewModel.RegisterLazy(() => new DocsLinkViewModel());
		OpenDataFolderViewModel.RegisterLazy(() => new OpenDataFolderViewModel());
		OpenWalletsFolderViewModel.RegisterLazy(() => new OpenWalletsFolderViewModel());
		OpenLogsViewModel.RegisterLazy(() => new OpenLogsViewModel());
		OpenTorLogsViewModel.RegisterLazy(() => new OpenTorLogsViewModel());
		OpenConfigFileViewModel.RegisterLazy(() => new OpenConfigFileViewModel());

		WalletCoinsViewModel.RegisterLazy(() =>
		{
			if (UiServices.WalletManager.TryGetSelectedAndLoggedInWalletViewModel(out var walletViewModel))
			{
				return new WalletCoinsViewModel(UiContext, walletViewModel);
			}

			return null;
		});

		CoinJoinSettingsViewModel.RegisterLazy(() =>
		{
			if (UiServices.WalletManager.TryGetSelectedAndLoggedInWalletViewModel(out var walletViewModel) && !walletViewModel.IsWatchOnly)
			{
				return walletViewModel.CoinJoinSettings;
			}

			return null;
		});

		WalletSettingsViewModel.RegisterLazy(() =>
		{
			if (UiServices.WalletManager.TryGetSelectedAndLoggedInWalletViewModel(out var walletViewModel))
			{
				return walletViewModel.Settings;
			}

			return null;
		});

		WalletStatsViewModel.RegisterLazy(() =>
		{
			if (UiServices.WalletManager.TryGetSelectedAndLoggedInWalletViewModel(out var walletViewModel))
			{
				return new WalletStatsViewModel(UiContext, walletViewModel);
			}

			return null;
		});

		WalletInfoViewModel.RegisterAsyncLazy(() =>
		{
			if (UiServices.WalletManager.TryGetSelectedAndLoggedInWalletViewModel(out var walletViewModel))
			{
				async Task<RoutableViewModel?> AuthorizeWalletInfo()
				{
					if (!string.IsNullOrEmpty(walletViewModel.Wallet.Kitchen.SaltSoup()))
					{
						var pwAuthDialog = new PasswordAuthDialogViewModel(walletViewModel.Wallet);
						var dialogResult = await UiContext.Navigate().NavigateDialogAsync(pwAuthDialog, NavigationTarget.CompactDialogScreen);

						if (!dialogResult.Result)
						{
							return null;
						}
					}

					return new WalletInfoViewModel(UiContext, walletViewModel);
				}

				return AuthorizeWalletInfo();
			}

			Task<RoutableViewModel?> NoWalletInfo() => Task.FromResult<RoutableViewModel?>(null);

			return NoWalletInfo();
		});

		SendViewModel.RegisterLazy(() =>
		{
			if (UiServices.WalletManager.TryGetSelectedAndLoggedInWalletViewModel(out var walletViewModel))
			{
				// TODO: Check if we can send?
				return new SendViewModel(UiContext, walletViewModel);
			}

			return null;
		});

		ReceiveViewModel.RegisterLazy(() =>
		{
			if (UiServices.WalletManager.TryGetSelectedAndLoggedInWalletViewModel(out var walletViewModel))
			{
				return new ReceiveViewModel(UiContext, new WalletModel(walletViewModel.Wallet));
			}

			return null;
		});
	}

	public void ApplyUiConfigWindowState()
	{
		WindowState = (WindowState)Enum.Parse(typeof(WindowState), Services.UiConfig.WindowState);
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Same lifecycle as the application. Won't be disposed separately.")]
	private SearchBarViewModel CreateSearchBar()
	{
		// This subject is created to solve the circular dependency between the sources and SearchBarViewModel
		var filterChanged = new Subject<string>();

		var source = new CompositeSearchSource(
			new ActionsSearchSource(UiContext, filterChanged),
			new SettingsSearchSource(_settingsPage, filterChanged),
			new TransactionsSearchSource(filterChanged));

		var searchBar = new SearchBarViewModel(source.Changes);

		searchBar
			.WhenAnyValue(a => a.SearchText)
			.WhereNotNull()
			.Subscribe(filterChanged);

		return searchBar;
	}
}
