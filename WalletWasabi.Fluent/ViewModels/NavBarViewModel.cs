using Avalonia.Threading;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using WalletWasabi.Gui;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels
{
	public class NavBarViewModel : ViewModelBase
	{
		private ObservableCollection<NavBarItemViewModel> _items;
		private ObservableCollection<NavBarItemViewModel> _topItems;
		private ObservableCollection<NavBarItemViewModel> _bottomItems;
		private NavBarItemViewModel _selectedItem;
		private Dictionary<Wallet, WalletViewModelBase> _walletDictionary;
		private bool _anyWalletStarted;		
		private bool _isExpanded;
		

		public NavBarViewModel(WalletManager walletManager, UiConfig uiConfig)
		{
			_topItems = new ObservableCollection<NavBarItemViewModel>();
			_items = new ObservableCollection<NavBarItemViewModel>();
			_bottomItems = new ObservableCollection<NavBarItemViewModel>();

			_walletDictionary = new Dictionary<Wallet, WalletViewModelBase>();
			
			_topItems.Add(new HomePageViewModel());
			_bottomItems.Add(new AddWalletPageViewModel());
			_bottomItems.Add(new SettingsPageViewModel());

			Observable
				.FromEventPattern<WalletState>(walletManager, nameof(WalletManager.WalletStateChanged))
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
							OpenClosedWallet(walletManager, uiConfig, cwvm);
						}
					}

					AnyWalletStarted = Items.OfType<WalletViewModelBase>().Any(x => x.WalletState == WalletState.Started);
				});

			Observable
				.FromEventPattern<Wallet>(walletManager, nameof(WalletManager.WalletAdded))
				.Select(x => x.EventArgs)
				.Where(x => x is { })
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(wallet =>
				{
					WalletViewModelBase vm = (wallet.State <= WalletState.Starting)
						? ClosedWalletViewModel.Create(walletManager, wallet)
						: WalletViewModel.Create(uiConfig, wallet);

					InsertWallet(vm);
				});

			Dispatcher.UIThread.Post(() =>
			{
				LoadWallets(walletManager);
			});
		}

		public bool AnyWalletStarted
		{
			get => _anyWalletStarted;
			set => this.RaiseAndSetIfChanged(ref _anyWalletStarted, value);
		}		

		public ObservableCollection<NavBarItemViewModel> TopItems
		{
			get { return _topItems; }
			set { this.RaiseAndSetIfChanged(ref _topItems, value); }
		}

		public ObservableCollection<NavBarItemViewModel> Items
		{
			get { return _items; }
			set { this.RaiseAndSetIfChanged(ref _items, value); }
		}		

		public ObservableCollection<NavBarItemViewModel> BottomItems
		{
			get { return _bottomItems; }
			set { this.RaiseAndSetIfChanged(ref _bottomItems, value); }
		}

		public NavBarItemViewModel SelectedItem
		{
			get { return _selectedItem; }
			set
			{
				_selectedItem = null;

				this.RaisePropertyChanged();

				this.RaiseAndSetIfChanged(ref _selectedItem, value);
			}
		}
 
 		public bool IsExpanded
		{
			get { return _isExpanded; }
			set { this.RaiseAndSetIfChanged(ref _isExpanded, value); }
		}

		private void LoadWallets(WalletManager walletManager)
		{
			foreach (var wallet in walletManager.GetWallets())
			{
				InsertWallet(ClosedWalletViewModel.Create(walletManager, wallet));
			}
		}

		private void OpenClosedWallet(WalletManager walletManager, UiConfig uiConfig, ClosedWalletViewModel closedWalletViewModel)
		{
			var select = SelectedItem == closedWalletViewModel;

			RemoveWallet(closedWalletViewModel);

			var walletViewModel = OpenWallet(walletManager, uiConfig, closedWalletViewModel.Wallet);

			if (select)
			{
				SelectedItem = walletViewModel;
			}
		}

		private WalletViewModelBase OpenWallet(WalletManager walletManager, UiConfig uiConfig, Wallet wallet)
		{
			if (_items.OfType<WalletViewModel>().Any(x => x.Title == wallet.WalletName))
			{
				throw new Exception("Wallet already opened.");
			}

			var walletViewModel = WalletViewModel.Create(uiConfig, wallet);

			InsertWallet(walletViewModel);

			if (!walletManager.AnyWallet(x => x.State >= WalletState.Started && x != walletViewModel.Wallet))
			{
				walletViewModel.OpenWalletTabs();
			}

			walletViewModel.IsExpanded = true;

			return walletViewModel;
		}

		private void InsertWallet(WalletViewModelBase walletVM)
		{
			Items.InsertSorted(walletVM);
			_walletDictionary.Add(walletVM.Wallet, walletVM);
		}

		internal void RemoveWallet(WalletViewModelBase walletVM)
		{
			walletVM.Dispose();

			_items.Remove(walletVM);
			_walletDictionary.Remove(walletVM.Wallet);
		}
	}
}
