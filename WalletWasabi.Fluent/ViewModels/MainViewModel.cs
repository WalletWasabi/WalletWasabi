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
using WalletWasabi.Legal;

namespace WalletWasabi.Fluent.ViewModels
{
	public partial class MainViewModel : ViewModelBase, IDialogHost
	{
		private readonly Global _global;
		[AutoNotify] private bool _isMainContentEnabled;
		[AutoNotify] private bool _isDialogScreenEnabled;
		[AutoNotify] private DialogViewModelBase? _currentDialog;
		[AutoNotify] private DialogScreenViewModel _dialogScreen;
		[AutoNotify] private NavBarViewModel _navBar;
		[AutoNotify] private StatusBarViewModel _statusBar;
		[AutoNotify] private string _title = "Wasabi Wallet";

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



			RegisterCategories(searchPage);

			HomePageViewModel.Register(async () => await Task.FromResult(homePage));

			SearchPageViewModel.Register(async () => searchPage);
			PrivacyModeViewModel.Register(async ()=> await Task.FromResult(privacyMode));
			AddWalletPageViewModel.Register(async () => await Task.FromResult(addWalletPage));

			SettingsPageViewModel.Register(async () =>
			{
				settingsPage.SelectedTab = 0;
				return await Task.FromResult(settingsPage);
			});

			GeneralSettingsTabViewModel.Register(async () =>
			{
				settingsPage.SelectedTab = 0;
				return await Task.FromResult(settingsPage);
			});

			PrivacySettingsTabViewModel.Register(async () =>
			{
				settingsPage.SelectedTab = 1;
				return await Task.FromResult(settingsPage);
			});

			NetworkSettingsTabViewModel.Register(async () =>
			{
				settingsPage.SelectedTab = 2;
				return await Task.FromResult(settingsPage);
			});

			BitcoinTabViewModel.Register(async () =>
			{
				settingsPage.SelectedTab = 3;
				return await Task.FromResult(settingsPage);
			});

			AboutViewModel.Register(async () => await Task.FromResult(new AboutViewModel()));

			_navBar.InitialiseAsync();

			searchPage.Initialise();

			MainScreen.To(homePage);

			this.WhenAnyValue(x => x.DialogScreen!.IsDialogOpen)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x => IsMainContentEnabled = !x);

			this.WhenAnyValue(x => x.CurrentDialog!.IsDialogOpen)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x => IsDialogScreenEnabled = !x);

			walletManager.WhenAnyValue(x => x.Items.Count)
				.Subscribe(x => _navBar.IsHidden = x == 0);
		}

		public TargettedNavigationStack MainScreen { get; }

		public static MainViewModel? Instance { get; internal set; }

		private Network Network { get; }

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
	}
}
