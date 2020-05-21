using System.Security.Cryptography.X509Certificates;
using System;
using AvalonStudio.Extensibility;
using AvalonStudio.MVVM;
using AvalonStudio.Shell;
using ReactiveUI;
using Splat;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Composition;
using System.Linq;
using System.Reactive.Linq;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Wallets;
using System.Reactive;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	[Export(typeof(IExtension))]
	[Export]
	[ExportToolControl]
	[Shared]
	public class WalletExplorerViewModel : ToolViewModel, IActivatableExtension
	{
		private ObservableCollection<WalletViewModelBase> _wallets;
		private ViewModelBase _selectedItem;
		private Dictionary<Wallet, WalletViewModelBase> _walletDictionary;
		private ObservableAsPropertyHelper<bool> _isLurkingWifeMode;
		private bool _anyWalletStarted;
		private bool _inSelecting;

		public WalletExplorerViewModel() : base("Wallet Explorer")
		{
			_wallets = new ObservableCollection<WalletViewModelBase>();

			_walletDictionary = new Dictionary<Wallet, WalletViewModelBase>();

			var global = Locator.Current.GetService<Global>();

			WalletManager = global.WalletManager;
			UiConfig = global.UiConfig;

			Observable
				.FromEventPattern<WalletState>(WalletManager, nameof(WalletManager.WalletStateChanged))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x =>
				{
					var wallet = x.Sender as Wallet;

					if (wallet is { } && _walletDictionary.ContainsKey(wallet))
					{
						if (wallet.State == WalletState.Stopping)
						{
							RemoveWallet(_walletDictionary[wallet]);
						}
						else if (_walletDictionary[wallet] is ClosedWalletViewModel cwvm && wallet.State == WalletState.Started)
						{
							OpenClosedWallet(cwvm);
						}
					}

					AnyWalletStarted = Wallets.Any(x => x.WalletState == WalletState.Started);
				});

			Observable
				.FromEventPattern<Wallet>(WalletManager, nameof(WalletManager.WalletAdded))
				.Select(x => x.EventArgs)
				.Where(x => x is { })
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(wallet =>
				{
					WalletViewModelBase vm = (wallet.State <= WalletState.Starting) ?
						ClosedWalletViewModel.Create(wallet) :
						WalletViewModel.Create(wallet);

					InsertWallet(vm);
				});

			CollapseAllCommand = ReactiveCommand.Create(CollapseWallets, this.WhenAnyValue(x => x.AnyWalletStarted));

			LurkingWifeModeCommand = ReactiveCommand.Create(ToggleLurkingWifeMode);

			_isLurkingWifeMode = UiConfig
				.WhenAnyValue(x => x.LurkingWifeMode)
				.ToProperty(this, x => x.IsLurkingWifeMode, scheduler: RxApp.MainThreadScheduler);

			Observable
				.Merge(CollapseAllCommand.ThrownExceptions)
				.Merge(LurkingWifeModeCommand.ThrownExceptions)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex => Logger.LogError(ex)); ;

			var shell = IoC.Get<IShell>();

			shell
				.WhenAnyValue(x => x.SelectedDocument)
				.OfType<ViewModelBase>()
				.Where(x => x != SelectedItem)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x => OnShellDocumentSelected(x));

			this.WhenAnyValue(x => x.SelectedItem)
				.OfType<WasabiDocumentTabViewModel>()
				.Where(_ => !_inSelecting)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x => shell.AddOrSelectDocument(x));

			this.WhenAnyValue(x => x.SelectedItem)
				.OfType<WalletViewModelBase>()
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x => x.IsExpanded = true);
		}

		public override Location DefaultLocation => Location.Right;

		public bool IsLurkingWifeMode => _isLurkingWifeMode?.Value ?? false;

		private WalletManager WalletManager { get; }

		private UiConfig UiConfig { get; }

		public ObservableCollection<WalletViewModelBase> Wallets
		{
			get => _wallets;
			set => this.RaiseAndSetIfChanged(ref _wallets, value);
		}

		public ViewModelBase SelectedItem
		{
			get => _selectedItem;
			set => this.RaiseAndSetIfChanged(ref _selectedItem, value);
		}

		public bool AnyWalletStarted
		{
			get => _anyWalletStarted;
			set => this.RaiseAndSetIfChanged(ref _anyWalletStarted, value);
		}

		public ReactiveCommand<Unit, Unit> CollapseAllCommand { get; }

		public ReactiveCommand<Unit, Unit> LurkingWifeModeCommand { get; }

		private void OnShellDocumentSelected(ViewModelBase document)
		{
			_inSelecting = true;

			try
			{
				SelectedItem = document;

				if (document is IWalletViewModel wvm && _walletDictionary.ContainsKey(wvm.Wallet))
				{
					_walletDictionary[wvm.Wallet].IsExpanded = true;
				}
			}
			finally
			{
				_inSelecting = false;
			}
		}

		private void ToggleLurkingWifeMode()
		{
			UiConfig.LurkingWifeMode = !UiConfig.LurkingWifeMode;
			UiConfig.ToFile();
		}

		private void CollapseWallets()
		{
			foreach (var wallet in Wallets)
			{
				wallet.IsExpanded = false;
			}
		}

		private void InsertWallet(WalletViewModelBase walletVM)
		{
			Wallets.InsertSorted(walletVM);
			_walletDictionary.Add(walletVM.Wallet, walletVM);
		}

		private void OpenClosedWallet(ClosedWalletViewModel closedWalletViewModel)
		{
			var select = SelectedItem == closedWalletViewModel;

			RemoveWallet(closedWalletViewModel);

			var walletViewModel = OpenWallet(closedWalletViewModel.Wallet);

			if (select)
			{
				SelectedItem = walletViewModel;
			}
		}

		internal WalletViewModelBase OpenWallet(Wallet wallet)
		{
			if (_wallets.OfType<WalletViewModel>().Any(x => x.Title == wallet.WalletName))
			{
				throw new Exception("Wallet already opened.");
			}

			var walletViewModel = WalletViewModel.Create(wallet);

			InsertWallet(walletViewModel);

			if (!WalletManager.AnyWallet(x => x.State >= WalletState.Started && x != walletViewModel.Wallet))
			{
				walletViewModel.OpenWalletTabs();
			}

			walletViewModel.IsExpanded = true;

			return walletViewModel;
		}

		internal void RemoveWallet(WalletViewModelBase walletVM)
		{
			walletVM.Dispose();

			Wallets.Remove(walletVM);
			_walletDictionary.Remove(walletVM.Wallet);
		}

		private void LoadWallets()
		{
			foreach (var wallet in WalletManager.GetWallets())
			{
				InsertWallet(ClosedWalletViewModel.Create(wallet));
			}
		}

		public void BeforeActivation()
		{
		}

		public void Activation()
		{
			IoC.Get<IShell>().MainPerspective.AddOrSelectTool(this);

			LoadWallets();
		}
	}
}
