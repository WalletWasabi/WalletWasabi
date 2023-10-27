using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Avalonia.Controls;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.AddWallet;
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

namespace WalletWasabi.Fluent.ViewModels;

public partial class MainViewModel : ViewModelBase
{
	private readonly SettingsPageViewModel _settingsPage;
	private readonly PrivacyModeViewModel _privacyMode;
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

		_settingsPage = new SettingsPageViewModel(UiContext);
		_privacyMode = new PrivacyModeViewModel(UiContext.ApplicationSettings);

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

				await UiContext.Navigate().To().WelcomePage().GetResultAsync();

				if (Services.WalletManager.HasWallet())
				{
					Services.UiConfig.Oobe = false;
					IsOobeBackgroundVisible = false;
				}
			}
		});

		SearchBar = CreateSearchBar();

		NetworkBadgeName = UiContext.ApplicationSettings.Network == Network.Main ? "" : UiContext.ApplicationSettings.Network.Name;

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

		if (UiContext.ApplicationSettings.Network != Network.Main)
		{
			Title += $" - {UiContext.ApplicationSettings.Network}";
		}
	}

	private void RegisterViewModels()
	{
		PrivacyModeViewModel.Register(_privacyMode);
		AddWalletPageViewModel.RegisterLazy(() => new AddWalletPageViewModel(UiContext));
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
		UserSupportViewModel.RegisterLazy(() => new UserSupportViewModel(UiContext));
		BugReportLinkViewModel.RegisterLazy(() => new BugReportLinkViewModel(UiContext));
		DocsLinkViewModel.RegisterLazy(() => new DocsLinkViewModel(UiContext));
		OpenDataFolderViewModel.RegisterLazy(() => new OpenDataFolderViewModel(UiContext));
		OpenWalletsFolderViewModel.RegisterLazy(() => new OpenWalletsFolderViewModel(UiContext));
		OpenLogsViewModel.RegisterLazy(() => new OpenLogsViewModel(UiContext));
		OpenTorLogsViewModel.RegisterLazy(() => new OpenTorLogsViewModel(UiContext));
		OpenConfigFileViewModel.RegisterLazy(() => new OpenConfigFileViewModel(UiContext));
	}

	public void ApplyUiConfigWindowState()
	{
		WindowState = (WindowState)Enum.Parse(typeof(WindowState), Services.UiConfig.WindowState);
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Same lifecycle as the application. Won't be disposed separately.")]
	private SearchBarViewModel CreateSearchBar()
	{
		// This subject is created to solve the circular dependency between the sources and SearchBarViewModel
		var querySubject = new Subject<string>();

		var source = new CompositeSearchSource(
			new ActionsSearchSource(UiContext, querySubject),
			new SettingsSearchSource(UiContext, querySubject),
			new TransactionsSearchSource(querySubject),
			UiContext.EditableSearchSource);

		var searchBar = new SearchBarViewModel(source.Changes);

		var queries = searchBar
			.WhenAnyValue(a => a.SearchText)
			.WhereNotNull();

		UiContext.EditableSearchSource.SetQueries(queries);

		queries
			.Subscribe(querySubject);

		return searchBar;
	}
}
