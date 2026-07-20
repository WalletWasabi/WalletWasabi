using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Avalonia.Controls;
using NBitcoin;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Services;
using WalletWasabi.Fluent.ViewModels.Dialogs.ReleaseHighlights;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.SearchBar;
using WalletWasabi.Fluent.ViewModels.SearchBar.Sources;
using WalletWasabi.Fluent.ViewModels.Settings;
using WalletWasabi.Fluent.ViewModels.StatusIcon;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Notifications;
using WalletWasabi.Helpers;
using System.Diagnostics.CodeAnalysis;
using WalletWasabi.Fluent.ViewModels.AddWallet;

namespace WalletWasabi.Fluent.ViewModels;

[AppLifetime]
public partial class MainViewModel : ViewModelBase
{
	[AutoNotify] private string _title = "Wasabi Wallet";
	[AutoNotify] private WindowState _windowState;
	[AutoNotify] private bool _isOobeBackgroundVisible;
	[AutoNotify] private bool _isCoinJoinActive;
	[AutoNotify] private string _activeMobileTab = "Wallets";
	[AutoNotify] private bool _isMobileShellNavVisible;

	public System.Windows.Input.ICommand NavigateToMobileWalletsCommand { get; }
	public System.Windows.Input.ICommand NavigateToMobileAddWalletCommand { get; }
	public System.Windows.Input.ICommand NavigateToMobileSettingsCommand { get; }

	public MainViewModel(UiContext uiContext) : base(uiContext)
	{
		UiContext.SetMainViewModel(this);

		ApplyUiConfigWindowState();

		DialogScreen = new DialogScreenViewModel(UiContext);
		FullScreen = new DialogScreenViewModel(UiContext, NavigationTarget.FullScreen);
		CompactDialogScreen = new DialogScreenViewModel(UiContext, NavigationTarget.CompactDialogScreen);
		NavBar = new NavBarViewModel(UiContext);
		MainScreen = new TargettedNavigationStack(UiContext, NavigationTarget.HomeScreen);
		UiContext.RegisterNavigation(new NavigationState(UiContext, MainScreen, DialogScreen, FullScreen, CompactDialogScreen, NavBar));

		NavBar.Activate();

		StatusIcon = new StatusIconViewModel(UiContext);

		SettingsPage = new SettingsPageViewModel(UiContext);
		PrivacyMode = new PrivacyModeViewModel(UiContext, UiContext.ApplicationSettings);
		Notifications = new WalletNotificationsViewModel(UiContext, NavBar);

		NavigationManager.RegisterType(NavBar);

		this.RegisterAllViewModels(UiContext);

		RxApp.MainThreadScheduler.Schedule(async () => await NavBar.InitialiseAsync());

		this.WhenAnyValue(x => x.WindowState)
			.Where(state => state != WindowState.Minimized)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(state => UiContext.ApplicationSettings.WindowState = state);

		IsMainContentEnabled =
			this.WhenAnyValue(
					x => x.DialogScreen.IsDialogOpen,
					x => x.FullScreen.IsDialogOpen,
					x => x.CompactDialogScreen.IsDialogOpen,
					(dialogIsOpen, fullScreenIsOpen, compactIsOpen) => !(dialogIsOpen || fullScreenIsOpen || compactIsOpen))
				.ObserveOn(RxApp.MainThreadScheduler);

		CurrentWallet =
			this.WhenAnyValue(x => x.MainScreen.CurrentPage)
				.WhereNotNull()
				.OfType<WalletViewModel>();

		IsOobeBackgroundVisible = UiContext.ApplicationSettings.Oobe;
		var isFirstLaunch = !UiContext.WalletRepository.HasWallet || UiContext.ApplicationSettings.Oobe;

		RxApp.MainThreadScheduler.Schedule(async () =>
		{
			if (isFirstLaunch)
			{
				IsOobeBackgroundVisible = true;

				await UiContext.Navigate().To().WelcomePage().GetResultAsync();

				if (UiContext.WalletRepository.HasWallet)
				{
					UiContext.ApplicationSettings.Oobe = false;
					IsOobeBackgroundVisible = false;
				}
			}

			await Task.Delay(1000);

			var lastVersionHighlightsDisplayed = UiContext.ApplicationSettings.LastVersionHighlightsDisplayed;
			UiContext.ApplicationSettings.LastVersionHighlightsDisplayed = Constants.ClientVersion;
			if (!isFirstLaunch && Constants.ClientVersion > lastVersionHighlightsDisplayed)
			{
				await uiContext.Navigate().NavigateDialogAsync(new ReleaseHighlightsDialogViewModel(UiContext),
					navigationMode: NavigationMode.Clear);
			}
		});

		SearchBar = CreateSearchBar();

		NetworkBadgeName =
			UiContext.ApplicationSettings.Network == Network.Main
			? ""
			: UiContext.ApplicationSettings.Network.Name;

		NavigateToMobileWalletsCommand = ReactiveUI.ReactiveCommand.Create(() =>
		{
			ActiveMobileTab = "Wallets";
			if (NavBar.SelectedWallet != null && NavBar.SelectedWallet.CurrentPage != null)
			{
				UiContext.Navigate().To(NavBar.SelectedWallet.CurrentPage, NavigationTarget.HomeScreen, NavigationMode.Clear);
			}
			else
			{
				UiContext.Navigate().To(new MobileWalletsListViewModel(UiContext), NavigationTarget.HomeScreen, NavigationMode.Clear);
			}
		});

		NavigateToMobileAddWalletCommand = ReactiveUI.ReactiveCommand.Create(() =>
		{
			ActiveMobileTab = "AddWallet";
			UiContext.Navigate().To(new AddWalletPageViewModel(UiContext), NavigationTarget.HomeScreen, NavigationMode.Clear);
		});

		NavigateToMobileSettingsCommand = ReactiveUI.ReactiveCommand.Create(() =>
		{
			ActiveMobileTab = "Settings";
			UiContext.Navigate().To(new SettingsPageViewModel(UiContext), NavigationTarget.HomeScreen, NavigationMode.Clear);
		});

		this.WhenAnyValue(x => x.MainScreen.CurrentPage)
			.Subscribe(page =>
			{
				if (page == null) return;
				var name = page.GetType().Name;
				if (name.Contains("Settings"))
				{
					ActiveMobileTab = "Settings";
				}
				else if (name.Contains("AddWallet") || name.Contains("Welcome"))
				{
					ActiveMobileTab = "AddWallet";
				}
				else
				{
					ActiveMobileTab = "Wallets";
				}
			});

		this.WhenAnyValue(x => x.MainScreen.CurrentPage, x => x.IsOobeBackgroundVisible)
			.Subscribe(t =>
			{
				var page = t.Item1;
				var isOobe = t.Item2;
				if (page == null || isOobe)
				{
					IsMobileShellNavVisible = false;
					return;
				}
				var name = page.GetType().Name;
				// Show only on main pages: Wallets list, Settings, Add Wallet landing page, or loaded Wallet Page/Dashboard
				IsMobileShellNavVisible = name.Contains("MobileWalletsList") ||
				                          name.Contains("SettingsPage") ||
				                          name.Contains("AddWalletPage") ||
				                          name.Contains("WalletPage") ||
				                          name.Contains("WalletViewModel");
			});
	}

	public IObservable<bool> IsMainContentEnabled { get; }

	public string NetworkBadgeName { get; }

	public IObservable<WalletViewModel> CurrentWallet { get; }

	public TargettedNavigationStack MainScreen { get; }

	public SearchBarViewModel SearchBar { get; }

	public DialogScreenViewModel DialogScreen { get; }
	public DialogScreenViewModel FullScreen { get; }
	public DialogScreenViewModel CompactDialogScreen { get; }
	public NavBarViewModel NavBar { get; }
	public StatusIconViewModel StatusIcon { get; }
	public SettingsPageViewModel SettingsPage { get; }
	public PrivacyModeViewModel PrivacyMode { get; }
	public WalletNotificationsViewModel Notifications { get; }

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

	public void Initialize()
	{
		UiContext.WalletRepository.Wallets
			.Connect()
			.FilterOnObservable(x => x.IsCoinjoinRunning)
			.ToCollection()
			.Select(x => x.Count != 0)
			.BindTo(this, x => x.IsCoinJoinActive);

		UiContext.Services.EventBus.AsObservable<RpcStatusChanged>()
			.Select(x => x.Status)
			.Where(x => !x.IsOk)
			.Take(1)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(x => NotificationHelpers.ShowError(
				"Could not connect to Bitcoin RPC",
				$"\n>>Click here to verify Bitcoin RPC settings.<<",
				onClick: () =>
				{
					SettingsPage.SelectedTab = 1; // Bitcoin Tab
					_ = SettingsPage.Activate();
				}));

		Notifications.StartListening();

		if (UiContext.ApplicationSettings.Network != Network.Main)
		{
			Title += $" - {UiContext.ApplicationSettings.Network}";
		}
	}

	public void ApplyUiConfigWindowState()
	{
		WindowState = UiContext.ApplicationSettings.WindowState;
	}

	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Same lifecycle as the application. Won't be disposed separately.")]
	private SearchBarViewModel CreateSearchBar()
	{
		// This subject is created to solve the circular dependency between the sources and SearchBarViewModel
		var querySubject = new Subject<string>();

		var source = new CompositeSearchSource(
			new ActionsSearchSource(UiContext, querySubject),
			new SettingsSearchSource(UiContext, querySubject),
			new TransactionsSearchSource(NavBar, querySubject),
			UiContext.EditableSearchSource);

		var searchBar = new SearchBarViewModel(source);

		var queries = searchBar
			.WhenAnyValue(a => a.SearchText)
			.WhereNotNull();

		UiContext.EditableSearchSource.SetQueries(queries);

		queries
			.Subscribe(querySubject);

		return searchBar;
	}
}
