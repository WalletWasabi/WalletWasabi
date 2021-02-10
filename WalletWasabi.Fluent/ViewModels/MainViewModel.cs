using System;
using System.Reactive.Concurrency;
using NBitcoin;
using ReactiveUI;
using System.Reactive.Linq;
using System.Threading.Tasks;
using WalletWasabi.Fluent.ViewModels.AddWallet;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using Global = WalletWasabi.Gui.Global;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Search;
using WalletWasabi.Fluent.ViewModels.Settings;
using WalletWasabi.Fluent.ViewModels.TransactionBroadcasting;
using WalletWasabi.Fluent.ViewModels.HelpAndSupport;
using WalletWasabi.Fluent.ViewModels.OpenDirectory;

namespace WalletWasabi.Fluent.ViewModels
{
	public partial class MainViewModel : ViewModelBase, IDialogHost
	{
		private readonly Global _global;
		[AutoNotify] private bool _isMainContentEnabled;
		[AutoNotify] private bool _isDialogScreenEnabled;
		[AutoNotify] private bool _isFullScreenEnabled;
		[AutoNotify] private DialogViewModelBase? _currentDialog;
		[AutoNotify] private DialogScreenViewModel _dialogScreen;
		[AutoNotify] private DialogScreenViewModel _fullScreen;
		[AutoNotify] private NavBarViewModel _navBar;
		[AutoNotify] private StatusBarViewModel _statusBar;
		[AutoNotify] private string _title = "Wasabi Wallet";
		private readonly SettingsPageViewModel _settingsPage;
		private readonly SearchPageViewModel _searchPage;
		private readonly PrivacyModeViewModel _privacyMode;
		private readonly AddWalletPageViewModel _addWalletPage;
		private readonly WalletManagerViewModel _walletManagerViewModel;

		public MainViewModel(Global global)
		{
			_global = global;

			_dialogScreen = new DialogScreenViewModel(800, 700);

			_fullScreen = new DialogScreenViewModel(double.PositiveInfinity, double.PositiveInfinity, NavigationTarget.FullScreen);

			MainScreen = new TargettedNavigationStack(NavigationTarget.HomeScreen);

			NavigationState.Register(MainScreen, DialogScreen, FullScreen, () => this);

			Network = global.Network;

			_currentDialog = null;

			_isMainContentEnabled = true;
			_isDialogScreenEnabled = true;
			_isFullScreenEnabled = true;

			_statusBar = new StatusBarViewModel(
				global.DataDir,
				global.Network,
				global.Config,
				global.HostedServices,
				global.BitcoinStore.SmartHeaderChain,
				global.Synchronizer);

			_walletManagerViewModel = new WalletManagerViewModel(global.WalletManager, global.UiConfig);

			_addWalletPage = new AddWalletPageViewModel(
				_walletManagerViewModel,
				global.BitcoinStore);

			_settingsPage = new SettingsPageViewModel(global.Config, global.UiConfig);
			_privacyMode = new PrivacyModeViewModel(global.UiConfig);
			_searchPage = new SearchPageViewModel();

			_navBar = new NavBarViewModel(MainScreen, _walletManagerViewModel);

			NavigationManager.RegisterType(_navBar);

			RegisterCategories(_searchPage);
			RegisterViewModels();

			RxApp.MainThreadScheduler.Schedule(async () => await _navBar.InitialiseAsync());

			_searchPage.Initialise();

			this.WhenAnyValue(x => x.DialogScreen!.IsDialogOpen)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x => IsMainContentEnabled = !x);

			this.WhenAnyValue(x => x.FullScreen!.IsDialogOpen)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x => IsMainContentEnabled = !x);

			this.WhenAnyValue(x => x.CurrentDialog!.IsDialogOpen)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x =>
				{
					IsFullScreenEnabled = !x;
					IsDialogScreenEnabled = !x;
				});

			_walletManagerViewModel.WhenAnyValue(x => x.Items.Count, x => x.Actions.Count)
				.Subscribe(x => _navBar.IsHidden = x.Item1 == 0 && x.Item2 == 0);

			if (!_walletManagerViewModel.Model.AnyWallet(_ => true))
			{
				MainScreen.To(_addWalletPage);
			}
		}

		public TargettedNavigationStack MainScreen { get; }

		public static MainViewModel? Instance { get; internal set; }

		private Network Network { get; }

		public void ClearStacks()
		{
			MainScreen.Clear();
			DialogScreen.Clear();
			FullScreen.Clear();
		}

		public void Initialize()
		{
			StatusBar.Initialize(_global.Nodes.ConnectedNodes);

			if (Network != Network.Main)
			{
				Title += $" - {Network}";
			}
		}

		private void RegisterViewModels()
		{
			SearchPageViewModel.Register(_searchPage);
			PrivacyModeViewModel.Register(_privacyMode);
			AddWalletPageViewModel.Register(_addWalletPage);
			SettingsPageViewModel.Register(_settingsPage);

			GeneralSettingsTabViewModel.RegisterLazy(
				() =>
				{
					_settingsPage.SelectedTab = 0;
					return _settingsPage;
				});

			PrivacySettingsTabViewModel.RegisterLazy(
				() =>
				{
					_settingsPage.SelectedTab = 1;
					return _settingsPage;
				});

			NetworkSettingsTabViewModel.RegisterLazy(
				() =>
				{
					_settingsPage.SelectedTab = 2;
					return _settingsPage;
				});

			BitcoinTabSettingsViewModel.RegisterLazy(
				() =>
				{
					_settingsPage.SelectedTab = 3;
					return _settingsPage;
				});

			AboutViewModel.RegisterLazy(() => new AboutViewModel());

			BroadcastTransactionViewModel.RegisterAsyncLazy(
				async () =>
				{
					var dialogResult = await DialogScreen.NavigateDialog(new LoadTransactionViewModel(_global.Network));

					if (dialogResult.Result is { })
					{
						while (_global.TransactionBroadcaster is null)
						{
							await Task.Delay(100);
						}

						return new BroadcastTransactionViewModel(
							_global.BitcoinStore,
							_global.Network,
							_global.TransactionBroadcaster,
							dialogResult.Result);
					}

					return null;
				});

			LegalDocumentsViewModel.RegisterLazy(() => new LegalDocumentsViewModel(_global.LegalChecker));

			UserSupportViewModel.RegisterLazy(() => new UserSupportViewModel());
			BugReportLinkViewModel.RegisterLazy(() => new BugReportLinkViewModel());
			DocsLinkViewModel.RegisterLazy(() => new DocsLinkViewModel());

			OpenDataFolderViewModel.RegisterLazy(() => new OpenDataFolderViewModel(_global.DataDir));
			OpenDirectory.OpenWalletsFolderViewModel.RegisterLazy(() => new OpenDirectory.OpenWalletsFolderViewModel(_walletManagerViewModel.Model.WalletDirectories.WalletsDir));
			OpenLogsViewModel.RegisterLazy(() => new OpenLogsViewModel());
			OpenTorLogsViewModel.RegisterLazy(() => new OpenTorLogsViewModel(_global));
			OpenConfigFileViewModel.RegisterLazy(() => new OpenConfigFileViewModel(_global));
		}

		private static void RegisterCategories(SearchPageViewModel searchPage)
		{
			searchPage.RegisterCategory("General", 0);
			searchPage.RegisterCategory("Settings", 1);
			searchPage.RegisterCategory("Help & Support", 2);
			searchPage.RegisterCategory("Open", 3);
		}
	}
}
