using System;
using NBitcoin;
using ReactiveUI;
using System.Reactive;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using Global = WalletWasabi.Gui.Global;

namespace WalletWasabi.Fluent.ViewModels
{
	public class MainViewModel : ViewModelBase, IScreen, IDialogHost
	{
		private readonly Global _global;
		private StatusBarViewModel _statusBar;
		private string _title = "Wasabi Wallet";
		private DialogViewModelBase? _currentDialog;
		private DialogScreenViewModel? _dialogScreen;
		private NavBarViewModel _navBar;
		private bool _isMainContentEnabled;
		private bool _isDialogEnabled;

		public MainViewModel(Global global)
		{
			_global = global;

			_dialogScreen = new DialogScreenViewModel();

			var navigationState = new NavigationStateViewModel()
			{
				HomeScreen = () => this,
				DialogScreen = () => _dialogScreen,
				DialogHost = () => this
			};

			Network = global.Network;

			_currentDialog = null;

			_isMainContentEnabled = true;
			_isDialogEnabled = true;

			_statusBar = new StatusBarViewModel(global.DataDir, global.Network, global.Config, global.HostedServices, global.BitcoinStore.SmartHeaderChain, global.Synchronizer, global.LegalDocuments);

			var walletManager = new WalletManagerViewModel(navigationState, global.WalletManager, global.UiConfig);

			var addWalletPage = new AddWalletPageViewModel(navigationState, global.WalletManager, global.BitcoinStore, global.Network);

			_navBar = new NavBarViewModel(navigationState, Router, walletManager, addWalletPage);

			this.WhenAnyValue(x => x.DialogScreen!.IsDialogVisible)
				.Subscribe(
					x => IsMainContentEnabled = !x);

			this.WhenAnyValue(x => x.CurrentDialog!.IsDialogOpen)
				.Subscribe(
					x => IsDialogEnabled = !x);
		}

		public bool IsMainContentEnabled
		{
			get => _isMainContentEnabled;
			set => this.RaiseAndSetIfChanged(ref _isMainContentEnabled, value);
		}

		public bool IsDialogEnabled
		{
			get => _isDialogEnabled;
			set => this.RaiseAndSetIfChanged(ref _isDialogEnabled, value);
		}

		public static MainViewModel? Instance { get; internal set; }

		public RoutingState Router { get; } = new RoutingState();

		public ReactiveCommand<Unit, Unit> GoBack => Router.NavigateBack;

		private Network Network { get; }

		public DialogScreenViewModel? DialogScreen
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
	}
}
