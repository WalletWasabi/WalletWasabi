using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Avalonia.Controls;
using NBitcoin;
using ReactiveUI;
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
	private readonly AddWalletPageViewModel _addWalletPage;
	[AutoNotify] private bool _isMainContentEnabled;
	[AutoNotify] private bool _isDialogScreenEnabled;
	[AutoNotify] private bool _isFullScreenEnabled;
	[AutoNotify] private DialogScreenViewModel _dialogScreen;
	[AutoNotify] private DialogScreenViewModel _fullScreen;
	[AutoNotify] private DialogScreenViewModel _compactDialogScreen;
	[AutoNotify] private NavBarViewModel _navBar;
	[AutoNotify] private StatusIconViewModel _statusIcon;
	[AutoNotify] private string _title = "Wasabi Wallet";
	[AutoNotify] private WindowState _windowState;
	[AutoNotify] private bool _isOobeBackgroundVisible;
	[AutoNotify] private bool _isCoinJoinActive;

	public MainViewModel()
	{
		ApplyUiConfigWindowSate();

		_dialogScreen = new DialogScreenViewModel();

		_fullScreen = new DialogScreenViewModel(NavigationTarget.FullScreen);

		_compactDialogScreen = new DialogScreenViewModel(NavigationTarget.CompactDialogScreen);

		MainScreen = new TargettedNavigationStack(NavigationTarget.HomeScreen);

		NavigationState.Register(MainScreen, DialogScreen, FullScreen, CompactDialogScreen);

		_isMainContentEnabled = true;
		_isDialogScreenEnabled = true;
		_isFullScreenEnabled = true;

		_statusIcon = new StatusIconViewModel();

		UiServices.Initialize();

		_addWalletPage = new AddWalletPageViewModel();
		_settingsPage = new SettingsPageViewModel();
		_privacyMode = new PrivacyModeViewModel();
		_navBar = new NavBarViewModel(MainScreen);

		NavigationManager.RegisterType(_navBar);
		RegisterViewModels();

		RxApp.MainThreadScheduler.Schedule(async () => await _navBar.InitialiseAsync());

		this.WhenAnyValue(x => x.WindowState)
			.Where(state => state != WindowState.Minimized)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(state => Services.UiConfig.WindowState = state.ToString());

		this.WhenAnyValue(
				x => x.DialogScreen!.IsDialogOpen,
				x => x.FullScreen!.IsDialogOpen,
				x => x.CompactDialogScreen!.IsDialogOpen)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(tup =>
			{
				var (dialogScreenIsOpen, fullScreenIsOpen, compactDialogScreenIsOpen) = tup;

				IsMainContentEnabled = !(dialogScreenIsOpen || fullScreenIsOpen || compactDialogScreenIsOpen);
			});

		this.WhenAnyValue(
				x => x.DialogScreen.CurrentPage,
				x => x.CompactDialogScreen.CurrentPage,
				x => x.FullScreen.CurrentPage,
				x => x.MainScreen.CurrentPage)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(tup =>
			{
				var (dialog, compactDialog, fullscreenDialog, mainsScreen) = tup;

				/*
				 * Order is important.
				 * Always the topmost content will be the active one.
				 */

				if (compactDialog is { })
				{
					compactDialog.SetActive();
					return;
				}

				if (dialog is { })
				{
					dialog.SetActive();
					return;
				}

				if (fullscreenDialog is { })
				{
					fullscreenDialog.SetActive();
					return;
				}

				if (mainsScreen is { })
				{
					mainsScreen.SetActive();
					return;
				}
			});

		CurrentWallet =
			this.WhenAnyValue(x => x.MainScreen.CurrentPage)
			.WhereNotNull()
			.OfType<WalletViewModel>();

		IsOobeBackgroundVisible = Services.UiConfig.Oobe;

		RxApp.MainThreadScheduler.Schedule(async () =>
		{
			if (!Services.WalletManager.HasWallet() || Services.UiConfig.Oobe)
			{
				IsOobeBackgroundVisible = true;

				await _dialogScreen.NavigateDialogAsync(new WelcomePageViewModel(_addWalletPage));

				if (Services.WalletManager.HasWallet())
				{
					Services.UiConfig.Oobe = false;
					IsOobeBackgroundVisible = false;
				}
			}
		});

		var source = new CompositeSearchItemsSource(new ActionsSource(), new SettingsSource(_settingsPage));
		SearchBar = new SearchBarViewModel(source.Changes);
	}

	public IObservable<WalletViewModel> CurrentWallet { get; }

	public TargettedNavigationStack MainScreen { get; }

	public SearchBarViewModel SearchBar { get; }

	public static MainViewModel Instance { get; } = new();

	public bool IsBusy =>
		MainScreen.CurrentPage is { IsBusy: true } ||
		DialogScreen.CurrentPage is { IsBusy: true } ||
		FullScreen.CurrentPage is { IsBusy: true } ||
		CompactDialogScreen.CurrentPage is { IsBusy: true };

	public void ClearStacks()
	{
		MainScreen.Clear();
		DialogScreen.Clear();
		FullScreen.Clear();
		CompactDialogScreen.Clear();
	}

	public void InvalidateIsCoinJoinActive()
	{
		IsCoinJoinActive = UiServices.WalletManager.Wallets.OfType<WalletViewModel>()
			.Any(x => x.IsCoinJoining);
	}

	public void Initialize()
	{
		StatusIcon.Initialize();

		if (Services.Config.Network != Network.Main)
		{
			Title += $" - {Services.Config.Network}";
		}
	}

	private void RegisterViewModels()
	{
		PrivacyModeViewModel.Register(_privacyMode);
		AddWalletPageViewModel.Register(_addWalletPage);
		SettingsPageViewModel.Register(_settingsPage);

		GeneralSettingsTabViewModel.RegisterLazy(
			() =>
			{
				_settingsPage.SelectedTab = 0;
				return _settingsPage;
			});

		BitcoinTabSettingsViewModel.RegisterLazy(
			() =>
			{
				_settingsPage.SelectedTab = 1;
				return _settingsPage;
			});

		AboutViewModel.RegisterLazy(() => new AboutViewModel());

		BroadcastTransactionViewModel.RegisterAsyncLazy(
			async () =>
			{
				var dialogResult = await DialogScreen.NavigateDialogAsync(new LoadTransactionViewModel(Services.Config.Network));

				if (dialogResult.Result is { })
				{
					return new BroadcastTransactionViewModel(Services.Config.Network,
						dialogResult.Result);
				}

				return null;
			});

		LegalDocumentsViewModel.RegisterLazy(() => new LegalDocumentsViewModel());
		UserSupportViewModel.RegisterLazy(() => new UserSupportViewModel());
		BugReportLinkViewModel.RegisterLazy(() => new BugReportLinkViewModel());
		DocsLinkViewModel.RegisterLazy(() => new DocsLinkViewModel());
		OpenDataFolderViewModel.RegisterLazy(() => new OpenDataFolderViewModel());
		OpenWalletsFolderViewModel.RegisterLazy(() => new OpenWalletsFolderViewModel());
		OpenLogsViewModel.RegisterLazy(() => new OpenLogsViewModel());
		OpenTorLogsViewModel.RegisterLazy(() => new OpenTorLogsViewModel());
		OpenConfigFileViewModel.RegisterLazy(() => new OpenConfigFileViewModel());
	}

	public void ApplyUiConfigWindowSate()
	{
		WindowState = (WindowState)Enum.Parse(typeof(WindowState), Services.UiConfig.WindowState);
	}
}
