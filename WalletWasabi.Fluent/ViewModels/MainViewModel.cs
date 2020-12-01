using System;
using System.IO;
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
using WalletWasabi.Fluent.ViewModels.TransactionBroadcaster;
using WalletWasabi.Legal;

namespace WalletWasabi.Fluent.ViewModels
{
	public class MainViewModel : ViewModelBase, IDialogHost
	{
		private readonly Global _global;
		private StatusBarViewModel _statusBar;
		private string _title = "Wasabi Wallet";
		private DialogViewModelBase? _currentDialog;
		private DialogScreenViewModel _dialogScreen;
		private NavBarViewModel _navBar;
		private bool _isMainContentEnabled;
		private bool _isDialogScreenEnabled;

		public MainViewModel(Global global)
		{
			_global = global;

			_dialogScreen = new DialogScreenViewModel();

			MainScreen = new TargettedNavigationStack(NavigationTarget.HomeScreen);

			NavigationState.Register(MainScreen, DialogScreen, () => this);

			Network = global.Network;

			_currentDialog = null;

			_isMainContentEnabled = true;
			_isDialogScreenEnabled = true;

			_statusBar = new StatusBarViewModel(global.DataDir, global.Network, global.Config, global.HostedServices, global.BitcoinStore.SmartHeaderChain, global.Synchronizer, global.LegalDocuments);

			var walletManager = new WalletManagerViewModel(global.WalletManager, global.UiConfig);

			var addWalletPage = new AddWalletPageViewModel(global.LegalDocuments, global.WalletManager, global.BitcoinStore, global.Network);

			var settingsPage = new SettingsPageViewModel(global.Config, global.UiConfig);

			var privacyMode = new PrivacyModeViewModel(global.UiConfig);

			var homePage = new HomePageViewModel(walletManager, addWalletPage);

			var searchPage = new SearchPageViewModel(walletManager);

			_navBar = new NavBarViewModel(MainScreen, walletManager);

			_navBar.RegisterTopItem(homePage);
			_navBar.RegisterBottomItem(searchPage);
			_navBar.RegisterBottomItem(privacyMode);
			_navBar.RegisterBottomItem(addWalletPage);
			_navBar.RegisterBottomItem(settingsPage);

			RegisterCategories(searchPage);
			RegisterRootEntries(searchPage, homePage, settingsPage, addWalletPage);
			RegisterEntries(searchPage, global.LegalDocuments);
			RegisterSettingsSearchItems(searchPage, settingsPage);

			searchPage.Initialise();

			MainScreen.To(homePage);

			this.WhenAnyValue(x => x.DialogScreen!.IsDialogOpen)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x => IsMainContentEnabled = !x);

			this.WhenAnyValue(x => x.CurrentDialog!.IsDialogOpen)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x => IsDialogScreenEnabled = !x);
		}

		public bool IsMainContentEnabled
		{
			get => _isMainContentEnabled;
			set => this.RaiseAndSetIfChanged(ref _isMainContentEnabled, value);
		}

		public bool IsDialogScreenEnabled
		{
			get => _isDialogScreenEnabled;
			set => this.RaiseAndSetIfChanged(ref _isDialogScreenEnabled, value);
		}

		public TargettedNavigationStack MainScreen { get; }

		public static MainViewModel? Instance { get; internal set; }

		private Network Network { get; }

		public DialogScreenViewModel DialogScreen
		{
			get => _dialogScreen;
			set => this.RaiseAndSetIfChanged(ref _dialogScreen, value);
		}

		public DialogViewModelBase? CurrentDialog
		{
			get => _currentDialog;
			set => this.RaiseAndSetIfChanged(ref _currentDialog, value);
		}

		public NavBarViewModel NavBar
		{
			get => _navBar;
			set => this.RaiseAndSetIfChanged(ref _navBar, value);
		}

		public StatusBarViewModel StatusBar
		{
			get => _statusBar;
			set => this.RaiseAndSetIfChanged(ref _statusBar, value);
		}

		public string Title
		{
			get => _title;
			internal set => this.RaiseAndSetIfChanged(ref _title, value);
		}

		public void Initialize()
		{
			// Temporary to keep things running without VM modifications.
			MainWindowViewModel.Instance = new MainWindowViewModel(_global.Network, _global.UiConfig, _global.WalletManager, null!, null!, false);

			StatusBar.Initialize(_global.Nodes.ConnectedNodes);

			if (Network != Network.Main)
			{
				Title += $" - {Network}";
			}
		}

		private static void RegisterCategories(SearchPageViewModel searchPage)
		{
			searchPage.RegisterCategory("General", 0);
			searchPage.RegisterCategory("Settings", 1);
		}

		private static void RegisterEntries(SearchPageViewModel searchPage, LegalDocuments legalDocuments)
		{
			searchPage.RegisterSearchEntry(
				title: "Legal Docs",
				caption: "Displays terms and conditions",
				order: 3,
				category: "General",
				keywords: "View, Legal, Docs, Documentation, Terms, Conditions, Help",
				iconName: "info_regular",
				createTargetView: async () =>
				{
					var content = await File.ReadAllTextAsync(legalDocuments.FilePath);

					var legalDocs = new LegalDocumentsViewModel(content);

					return legalDocs;
				});

			searchPage.RegisterSearchEntry(
				title: "About Wasabi",
				caption: "Displays all the current info about the app",
				order: 4,
				category: "General",
				keywords: "About, Software, Version, Source Code, Github, Status, Stats, Tor, Onion, Bug, Report, FAQ, Questions," +
				          "Docs, Documentation, Link, Links, Help",
				iconName: "info_regular",
				createTargetView: async () =>  await Task.FromResult(new AboutViewModel()));

			searchPage.RegisterSearchEntry(
				title: "Broadcaster",
				caption: "Broadcast your transactions here",
				order: 5,
				category: "General",
				keywords: "Transaction Id, Input, Output, Amount, Network, Fee, Count, BTC, Signed, Paste, Import, Broadcast, Transaction",
				iconName: "live_regular",
				createTargetView: async () =>  await Task.FromResult(new LoadTransactionViewModel()));
		}

		private static void RegisterRootEntries(
			SearchPageViewModel searchPage,
			HomePageViewModel homePage,
			SettingsPageViewModel settingsPage,
			AddWalletPageViewModel addWalletPage)
		{
			searchPage.RegisterSearchEntry(
				"Home",
				"Manage existing wallets",
				0,
				"General",
				"Home",
				"home_regular",
				async () =>  await Task.FromResult(homePage));

			searchPage.RegisterSearchEntry(
				title: "Settings",
				caption: "Manage appearance, privacy and other settings",
				order: 1,
				category: "General",
				keywords: "Settings, General, User Interface, Privacy, Advanced",
				iconName: "settings_regular",
				createTargetView: async () =>  await Task.FromResult(settingsPage));

			searchPage.RegisterSearchEntry(
				title: "Add Wallet",
				caption: "Create, recover or import wallet",
				order: 2,
				category: "General",
				keywords: "Wallet, Add Wallet, Create Wallet, Recover Wallet, Import Wallet, Connect Hardware Wallet",
				iconName: "add_circle_regular",
				createTargetView: async () =>  await Task.FromResult(addWalletPage));
		}

		private static void RegisterSettingsSearchItems(SearchPageViewModel searchPage, SettingsPageViewModel settingsPage)
		{
			searchPage.RegisterSearchEntry(
				title: "General",
				caption: "Manage general settings",
				order: 0,
				category: "Settings",
				keywords: "Settings, General, Dark Mode, Bitcoin Addresses, Manual Entry Free, Custom Change Address, Fee Display Format, Dust Threshold, BTC",
				iconName: "settings_general_regular",
				createTargetView: async () =>
				{
					settingsPage.SelectedTab = 0;
					return await Task.FromResult(settingsPage);
				});

			searchPage.RegisterSearchEntry(
				title: "Privacy",
				caption: "Manage privacy settings",
				order: 1,
				category: "Settings",
				keywords: "Settings, Privacy, Minimal, Medium, Strong, Anonymity Level",
				iconName: "settings_privacy_regular",
				createTargetView: async () =>
				{
					settingsPage.SelectedTab = 1;
					return await Task.FromResult(settingsPage);
				});

			searchPage.RegisterSearchEntry(
				title: "Network",
				caption: "Manage network settings",
				order: 2,
				category: "Settings",
				keywords: "Settings, Network, Encryption, Tor, Terminate, Wasabi, Shutdown, SOCKS5, Endpoint",
				iconName: "settings_network_regular",
				createTargetView: async () =>
				{
					settingsPage.SelectedTab = 2;
					return await Task.FromResult(settingsPage);
				});

			searchPage.RegisterSearchEntry(
				title: "Bitcoin",
				caption: "Manage Bitcoin settings",
				order: 3,
				category: "Settings",
				keywords: "Settings, Bitcoin, Network, Main, TestNet, RegTest, Run, Knots, Startup, P2P, Endpoint",
				iconName: "settings_bitcoin_regular",
				createTargetView: async () =>
				{
					settingsPage.SelectedTab = 3;
					return await Task.FromResult(settingsPage);
				});
		}
	}
}
