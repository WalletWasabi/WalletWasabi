using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using Avalonia.Threading;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Actions;
using WalletWasabi.Gui;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels
{
	public partial class WalletManagerViewModel : ViewModelBase
	{
		private readonly Dictionary<Wallet, WalletViewModelBase> _walletDictionary;
		private readonly Dictionary<WalletViewModelBase, List<NavBarItemViewModel>> _walletActionsDictionary;
		[AutoNotify] private WalletViewModelBase? _selectedWallet;
		[AutoNotify] private bool _loggedInAndSelectedAlwaysFirst;
		[AutoNotify] private ObservableCollection<NavBarItemViewModel> _items;
		[AutoNotify] private ObservableCollection<WalletViewModelBase> _wallets;
		[AutoNotify] private bool _anyWalletStarted;

		public WalletManagerViewModel(WalletManager walletManager, UiConfig uiConfig)
		{
			Model = walletManager;
			_walletDictionary = new Dictionary<Wallet, WalletViewModelBase>();
			_walletActionsDictionary = new Dictionary<WalletViewModelBase, List<NavBarItemViewModel>>();
			_items = new ObservableCollection<NavBarItemViewModel>();
			_wallets = new ObservableCollection<WalletViewModelBase>();
			_loggedInAndSelectedAlwaysFirst = true;

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
			RemoveWallet(closedWalletViewModel);

			var walletViewModel = OpenWallet(walletManager, uiConfig, closedWalletViewModel.Wallet);

			// TODO: Handle walletViewModel
		}

		private WalletViewModelBase OpenWallet(WalletManager walletManager, UiConfig uiConfig, Wallet wallet)
		{
			if (_wallets.OfType<WalletViewModel>().Any(x => x.Title == wallet.WalletName))
			{
				throw new Exception("Wallet already opened.");
			}

			var walletViewModel = WalletViewModel.Create(uiConfig, wallet);

			InsertWallet(walletViewModel);

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

		public void SelectionChanged(NavBarItemViewModel item)
		{
			if (SelectedWallet == item)
			{
				return;
			}

			if (SelectedWallet is WalletViewModelBase walletViewModelPrevious && walletViewModelPrevious.Wallet.IsLoggedIn)
			{
				if (item is WalletViewModelBase && SelectedWallet != item)
				{
					RemoveActions(walletViewModelPrevious);

					SelectedWallet = null;
				}
			}

			if (item is WalletViewModelBase walletViewModelSelected && walletViewModelSelected.Wallet.IsLoggedIn)
			{
				if (!_walletActionsDictionary.TryGetValue(walletViewModelSelected, out var actions))
				{
					actions = new List<NavBarItemViewModel>();
				}

				_walletActionsDictionary[walletViewModelSelected] = actions;
				InsertActions(walletViewModelSelected, actions);

				SelectedWallet = walletViewModelSelected;
			}
		}

		private List<WalletActionViewModel> GetWalletActions(WalletViewModelBase walletViewModel)
		{
			var wallet = walletViewModel.Wallet;
			var actions = new List<WalletActionViewModel>();

			if (wallet.KeyManager.IsHardwareWallet || !wallet.KeyManager.IsWatchOnly)
			{
				actions.Add(new SendWalletActionViewModel(walletViewModel));
			}

			actions.Add(new ReceiveWalletActionViewModel(walletViewModel));

			if (!wallet.KeyManager.IsWatchOnly)
			{
				actions.Add(new CoinJoinWalletActionViewModel(walletViewModel));
			}

			actions.Add(new AdvancedWalletActionViewModel(walletViewModel));

			return actions;
		}

		private void InsertActions(WalletViewModelBase walletViewModel, List<NavBarItemViewModel> result)
		{
			// Insert current lodged in wallet at the top of the items list.
			if (_loggedInAndSelectedAlwaysFirst)
			{
				_items.Remove(walletViewModel);
				_items.Insert(0, walletViewModel);

				_wallets.Remove(walletViewModel);
				_wallets.Insert(0, walletViewModel);
			}

			var index = _wallets.IndexOf(walletViewModel);
			if (index >= 0)
			{
				var insertIndex = index;
				// Add top separator only when wallet is not first item.
				if (index > 0)
				{
					var topSeparator = new SeparatorItemViewModel();
					_items.Insert(index, topSeparator);
					result.Add(topSeparator);
					insertIndex += 1;
				}

				var actions = GetWalletActions(walletViewModel);

				for (var i = 0; i < actions.Count; i++)
				{
					var action = actions[i];
					_items.Insert(insertIndex + i + 1, action);
					result.Add(action);
				}

				// Add bottom separator only when wallet is not first or last item.
				if (_wallets.Count > 1 && index != _wallets.Count - 1)
				{
					var bottomSeparator = new SeparatorItemViewModel();
					_items.Insert(insertIndex + actions.Count + 1, bottomSeparator);
					result.Add(bottomSeparator);
				}
			}
		}

		private void RemoveActions(WalletViewModelBase walletViewModelPrevious)
		{
			var actions = _walletActionsDictionary[walletViewModelPrevious];
			foreach (var action in actions)
			{
				_items.Remove(action);
			}

			actions.Clear();
		}
	}
}