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

		public override Location DefaultLocation => Location.Right;

		public WalletExplorerViewModel()
		{
			Title = "Wallet Explorer";

			_wallets = new ObservableCollection<WalletViewModelBase>();

			_walletDictionary = new Dictionary<Wallet, WalletViewModelBase>();

			WalletManager = Locator.Current.GetService<Global>().WalletManager;

			Observable.FromEventPattern<WalletState>(WalletManager, nameof(WalletManager.WalletStateChanged))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x =>
				{
					var wallet = x.Sender as Wallet;

					if (wallet is { } && _walletDictionary.ContainsKey(wallet))
					{
						if (x.EventArgs == WalletState.Stopping)
						{
							RemoveWallet(_walletDictionary[wallet]);
						}
						else if (_walletDictionary[wallet] is ClosedWalletViewModel cwvm && x.EventArgs == WalletState.Started)
						{
							OpenClosedWallet(cwvm);
						}
					}
				});

			Observable.FromEventPattern<Wallet>(WalletManager, nameof(WalletManager.WalletAdded))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Select(x => x.EventArgs)
				.Where(x => x is { })
				.Subscribe(wallet =>
				{
					if (wallet.State <= WalletState.Starting)
					{
						Wallets.InsertSorted(new ClosedWalletViewModel(wallet));
					}
					else
					{
						Wallets.InsertSorted(new WalletViewModel(wallet));
					}
				});

			CollapseAllCommand = ReactiveCommand.Create(() =>
			{
				foreach (var wallet in Wallets)
				{
					wallet.IsExpanded = false;
				}
			});

			LurkingWifeModeCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				var uiConfig = Locator.Current.GetService<Global>().UiConfig;
				uiConfig.LurkingWifeMode = !uiConfig.LurkingWifeMode;
				uiConfig.ToFile();
			});

			LurkingWifeModeCommand.ThrownExceptions
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex => Logger.LogError(ex));

			var shell = IoC.Get<IShell>();

			shell.WhenAnyValue(x => x.SelectedDocument)
				.Subscribe(x =>
				{
					if (x is ViewModelBase vmb)
					{
						SelectedItem = vmb;
					}
				});

			this.WhenAnyValue(x => x.SelectedItem)
				.OfType<WasabiDocumentTabViewModel>()
				.Subscribe(x =>
				{
					shell.AddOrSelectDocument(x);
				});
		}

		private WalletManager WalletManager { get; }

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

		public ReactiveCommand<Unit, Unit> CollapseAllCommand { get; }

		public ReactiveCommand<Unit, Unit> LurkingWifeModeCommand { get; }

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
				throw new System.Exception("Wallet already opened.");
			}

			var walletViewModel = new WalletViewModel(wallet);

			InsertWallet(walletViewModel);

			if (!WalletManager.AnyWallet(x => x.State >= WalletState.Starting && x != walletViewModel.Wallet))
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
			foreach (var wallet in WalletManager.GetKeyManagers())
			{
				InsertWallet(new ClosedWalletViewModel(WalletManager.GetWalletByName(wallet.WalletName)));
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
