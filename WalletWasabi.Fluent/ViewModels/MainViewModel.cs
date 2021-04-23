using System;
using System.Reactive.Concurrency;
using NBitcoin;
using ReactiveUI;
using System.Reactive.Linq;
using WalletWasabi.Fluent.ViewModels.AddWallet;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using Global = WalletWasabi.Gui.Global;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Search;
using WalletWasabi.Fluent.ViewModels.Settings;
using WalletWasabi.Fluent.ViewModels.TransactionBroadcasting;
using WalletWasabi.Fluent.ViewModels.HelpAndSupport;
using WalletWasabi.Fluent.ViewModels.OpenDirectory;
using WalletWasabi.Services;
using WalletWasabi.Logging;
using WalletWasabi.BitcoinP2p;

namespace WalletWasabi.Fluent.ViewModels
{
	public partial class MainViewModel : ViewModelBase
	{
		private readonly Global _global;
		private readonly LegalChecker _legalChecker;
		[AutoNotify] private bool _isMainContentEnabled;
		[AutoNotify] private bool _isDialogScreenEnabled;
		[AutoNotify] private bool _isFullScreenEnabled;
		[AutoNotify] private DialogScreenViewModel _dialogScreen;
		[AutoNotify] private DialogScreenViewModel _fullScreen;
		[AutoNotify] private DialogScreenViewModel _compactDialogScreen;
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
			_legalChecker = global.LegalChecker;

			_dialogScreen = new DialogScreenViewModel();

			_fullScreen = new DialogScreenViewModel(NavigationTarget.FullScreen);

			_compactDialogScreen = new DialogScreenViewModel(NavigationTarget.CompactDialogScreen);

			MainScreen = new TargettedNavigationStack(NavigationTarget.HomeScreen);

			NavigationState.Register(MainScreen, DialogScreen, FullScreen, CompactDialogScreen);

			Network = global.Network;

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

			_walletManagerViewModel = new WalletManagerViewModel(
				_global.WalletManager,
				_global.UiConfig,
				_global.Config,
				_global.BitcoinStore,
				_global.LegalChecker,
				_global.TransactionBroadcaster,
				_global.HttpClientFactory);

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

			this.WhenAnyValue(x => x.CompactDialogScreen!.IsDialogOpen)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x => IsMainContentEnabled = !x);

			_walletManagerViewModel.WhenAnyValue(x => x.Items.Count, x => x.Actions.Count)
				.Subscribe(x => _navBar.IsHidden = x.Item1 == 0 && x.Item2 == 0);

			if (!_walletManagerViewModel.WalletManager.AnyWallet(_ => true))
			{
				_addWalletPage.Navigate().To(_addWalletPage, NavigationMode.Clear);
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
			StatusBar.Initialize(_global.HostedServices.Get<P2pNetwork>().Nodes.ConnectedNodes);

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
						return new BroadcastTransactionViewModel(
							_global.BitcoinStore,
							_global.Network,
							_global.TransactionBroadcaster,
							dialogResult.Result);
					}

					return null;
				});

			RxApp.MainThreadScheduler.Schedule(async () =>
			{
				try
				{
					await _legalChecker.WaitAndGetLatestDocumentAsync();

					LegalDocumentsViewModel.RegisterAsyncLazy(async () =>
					{
						var document = await _legalChecker.WaitAndGetLatestDocumentAsync();
						return new LegalDocumentsViewModel(document.Content);
					});
					_searchPage.RegisterSearchEntry(LegalDocumentsViewModel.MetaData);
				}
				catch (Exception ex)
				{
					if (ex is not OperationCanceledException)
					{
						Logger.LogError("Failed to get Legal documents.", ex);
					}
				}
			});

			UserSupportViewModel.RegisterLazy(() => new UserSupportViewModel());
			BugReportLinkViewModel.RegisterLazy(() => new BugReportLinkViewModel());
			DocsLinkViewModel.RegisterLazy(() => new DocsLinkViewModel());
			OpenDataFolderViewModel.RegisterLazy(() => new OpenDataFolderViewModel(_global.DataDir));
			OpenWalletsFolderViewModel.RegisterLazy(() => new OpenWalletsFolderViewModel(_walletManagerViewModel.WalletManager.WalletDirectories.WalletsDir));
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
