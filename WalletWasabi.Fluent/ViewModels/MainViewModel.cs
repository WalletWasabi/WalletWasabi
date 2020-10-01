using System;
using Avalonia.Threading;
using NBitcoin;
using ReactiveUI;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using WalletWasabi.Gui.ViewModels;
using Global = WalletWasabi.Gui.Global;

namespace WalletWasabi.Fluent.ViewModels
{
	public class MainViewModel : ViewModelBase, IScreen
	{
		private Global _global;
		private StatusBarViewModel _statusBar;
		private string _title = "Wasabi Wallet";
		private bool _isBackButtonVisible;

		public MainViewModel(Global global)
		{
			_global = global;

			Network = global.Network;

			StatusBar = new StatusBarViewModel(global.DataDir, global.Network, global.Config, global.HostedServices, global.BitcoinStore.SmartHeaderChain, global.Synchronizer, global.LegalDocuments);

			Dispatcher.UIThread.Post(async () =>
			{
				await Task.Delay(5000);

				Router.Navigate.Execute(new HomeViewModel(this));
			});

			Observable.FromEventPattern(Router.NavigationStack, nameof(Router.NavigationStack.CollectionChanged))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => IsBackButtonVisible = Router.NavigationStack.Count > 1);

			Router.Navigate.Execute(new HomeViewModel(this)); // for testing
			Router.Navigate.Execute(new HomeViewModel(this)); // for testing
		}

		public static MainViewModel Instance { get; internal set; }

		public RoutingState Router { get; } = new RoutingState();
		
		public ReactiveCommand<Unit, Unit> GoBack => Router.NavigateBack;

		public Network Network { get; }		

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

		public bool IsBackButtonVisible
		{
			get => _isBackButtonVisible;
			set => this.RaiseAndSetIfChanged(ref _isBackButtonVisible, value);
		}

		public void Initialize()
		{
			// Temporary to keep things running without VM modifications.
			MainWindowViewModel.Instance = new MainWindowViewModel(_global.Network, _global.UiConfig, _global.WalletManager, null, null, false);

			StatusBar.Initialize(_global.Nodes.ConnectedNodes);

			if (Network != Network.Main)
			{
				Title += $" - {Network}";
			}
		}
	}
}
