using NBitcoin;
using ReactiveUI;
using System.Reactive;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using Global = WalletWasabi.Gui.Global;
using WalletWasabi.Fluent.ViewModels.NavBar;

namespace WalletWasabi.Fluent.ViewModels
{
	public class MainViewModel : ViewModelBase, IScreen, IDialogHost
	{
		private readonly Global _global;
		private StatusBarViewModel _statusBar;
		private string _title = "Wasabi Wallet";
		private DialogViewModelBase? _currentDialog;
		private NavBarViewModel _navBar;

		public MainViewModel(Global global)
		{
			_global = global;
			Network = global.Network;

			_currentDialog = null;

			_statusBar = new StatusBarViewModel(global.DataDir, global.Network, global.Config, global.HostedServices, global.BitcoinStore.SmartHeaderChain, global.Synchronizer, global.LegalDocuments);

			var walletManager = new WalletManagerViewModel(this, global.WalletManager, global.UiConfig);

			var addWalletPage = new AddWalletPageViewModel(this, global.WalletManager, global.BitcoinStore, global.Network);

			_navBar = new NavBarViewModel(this, Router, walletManager, addWalletPage);
		}

		public static MainViewModel? Instance { get; internal set; }

		public RoutingState Router { get; } = new RoutingState();

		public ReactiveCommand<Unit, Unit> GoBack => Router.NavigateBack;

		private Network Network { get; }

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
