using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using Avalonia.Threading;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Gui;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels
{
	public partial class WalletManagerViewModel : ViewModelBase
	{
		private readonly Dictionary<Wallet, WalletViewModelBase> _walletDictionary;
		[AutoNotify] private ViewModelBase? _selectedItem;
		[AutoNotify] private ObservableCollection<NavBarItemViewModel> _items;
		[AutoNotify] private ObservableCollection<WalletViewModelBase> _wallets;
		[AutoNotify] private bool _anyWalletStarted;

		public WalletManagerViewModel(WalletManager walletManager, UiConfig uiConfig)
		{
			Model = walletManager;
			_walletDictionary = new Dictionary<Wallet, WalletViewModelBase>();
			_items = new ObservableCollection<NavBarItemViewModel>();
			_wallets = new ObservableCollection<WalletViewModelBase>();

			Observable
				.FromEventPattern<WalletState>(walletManager, nameof(WalletManager.WalletStateChanged))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(
					x =>
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

					AnyWalletStarted = Items.OfType<WalletViewModelBase>().Any(y => y.WalletState == WalletState.Started);
				});

			Observable
				.FromEventPattern<Wallet>(walletManager, nameof(WalletManager.WalletAdded))
				.Select(x => x.EventArgs)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(
					wallet =>
				{
					WalletViewModelBase vm = (wallet.State <= WalletState.Starting)
						? ClosedWalletViewModel.Create(walletManager, wallet)
						: WalletViewModel.Create(uiConfig, wallet);

					InsertWallet(vm);
				});

			Dispatcher.UIThread.Post(() => LoadWallets(walletManager));
		}

		public WalletManager Model { get; }

		private void OpenClosedWallet(WalletManager walletManager, UiConfig uiConfig, ClosedWalletViewModel closedWalletViewModel)
		{
			var select = SelectedItem == closedWalletViewModel;

			RemoveWallet(closedWalletViewModel);

			var walletViewModel = OpenWallet(walletManager, uiConfig, closedWalletViewModel.Wallet);

			// TODO: TEMP
			walletViewModel.Navigate().Clear();
			walletViewModel.Navigate(NavigationTarget.HomeScreen).To(walletViewModel);

			if (select)
			{
				SelectedItem = walletViewModel;
			}
		}

		private WalletViewModelBase OpenWallet(WalletManager walletManager, UiConfig uiConfig, Wallet wallet)
		{
			if (_wallets.OfType<WalletViewModel>().Any(x => x.Title == wallet.WalletName))
			{
				throw new Exception("Wallet already opened.");
			}

			var walletViewModel = WalletViewModel.Create(uiConfig, wallet);

			InsertWallet(walletViewModel);

			if (!walletManager.AnyWallet(x => x.State >= WalletState.Started && x != walletViewModel.Wallet))
			{
				walletViewModel.OpenWalletTabs();

				// TODO: TEMP
				var index = _wallets.IndexOf(walletViewModel);
				if (index >= 0)
				{
					for (var i = 0; i < walletViewModel.Actions.Count; i++)
					{
						var action = walletViewModel.Actions[i];
						_items.Insert(index + i + 1, action);
					}
				}
			}

			walletViewModel.IsExpanded = true;

			return walletViewModel;
		}

		private void InsertWallet(WalletViewModelBase wallet)
		{
			_wallets.InsertSorted(wallet);

			// TODO: Handle wallet Action being present in Items collection.
			var index = _wallets.IndexOf(wallet);
			if (index >= 0)
			{
				_items.Insert(index, wallet);
			}

			_walletDictionary.Add(wallet.Wallet, wallet);
		}

		private void RemoveWallet(WalletViewModelBase wallet)
		{
			wallet.Dispose();

			_wallets.Remove(wallet);
			_items.Remove(wallet);
			// TODO: Remove wallet Actions

			_walletDictionary.Remove(wallet.Wallet);
		}

		private void LoadWallets(WalletManager walletManager)
		{
			foreach (var wallet in walletManager.GetWallets())
			{
				InsertWallet(ClosedWalletViewModel.Create(walletManager, wallet));
			}
		}
	}
}