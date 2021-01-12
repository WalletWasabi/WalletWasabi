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
		[AutoNotify] private ObservableCollection<NavBarItemViewModel> _actions;
		[AutoNotify] private ObservableCollection<WalletViewModelBase> _wallets;
		[AutoNotify] private bool _anyWalletStarted;

		public WalletManagerViewModel(WalletManager walletManager, UiConfig uiConfig)
		{
			Model = walletManager;
			_walletDictionary = new Dictionary<Wallet, WalletViewModelBase>();
			_walletActionsDictionary = new Dictionary<WalletViewModelBase, List<NavBarItemViewModel>>();
			_items = new ObservableCollection<NavBarItemViewModel>();
			_actions = new ObservableCollection<NavBarItemViewModel>();
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

			var index = _wallets.IndexOf(wallet);
			if (index >= 0)
			{
				_items.Insert(index, wallet);
			}

			_walletDictionary.Add(wallet.Wallet, wallet);
		}

		private void RemoveWallet(WalletViewModelBase wallet)
		{
			var isLoggedIn = wallet.Wallet.IsLoggedIn;

			wallet.Dispose();

			_wallets.Remove(wallet);
			_items.Remove(wallet);

			if (isLoggedIn)
			{
				RemoveActions(wallet, true);
			}

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

			if (SelectedWallet is { IsLoggedIn: true } walletViewModelPrevious /*&& item is WalletViewModelBase { IsLoggedIn: true }*/)
			{
				if (item is not WalletActionViewModel && SelectedWallet != item)
				{
					//if (item is WalletViewModelBase && SelectedWallet != item)
					{
						RemoveActions(walletViewModelPrevious);

						SelectedWallet = null;
					}
				}
			}

			if (item is WalletViewModelBase { IsLoggedIn: true} walletViewModelItem)
			{
				if (!_walletActionsDictionary.TryGetValue(walletViewModelItem, out var actions))
				{
					actions = new List<NavBarItemViewModel>();
					_walletActionsDictionary[walletViewModelItem] = actions;
				}

				InsertActions(walletViewModelItem, actions);

				SelectedWallet = walletViewModelItem;
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
#if false
			if (_loggedInAndSelectedAlwaysFirst)
			{
				_items.Remove(walletViewModel);
				_items.Insert(0, walletViewModel);

				_wallets.Remove(walletViewModel);
				_wallets.Insert(0, walletViewModel);
			}
#else
			_items.Remove(walletViewModel);
			_actions.Add(walletViewModel);
#endif
			//var index = _wallets.IndexOf(walletViewModel);
			var index = 1;
			if (index >= 0)
			{
				var insertIndex = index;
				// Add top separator only when wallet is not first item.
#if false
				if (index > 0)
				{
					var topSeparator = new SeparatorItemViewModel();
					_items.Insert(index, topSeparator);
					result.Add(topSeparator);
					insertIndex += 1;
				}
#endif
				var actions = GetWalletActions(walletViewModel);

				for (var i = 0; i < actions.Count; i++)
				{
					var action = actions[i];
#if false
					_items.Insert(insertIndex + i + 1, action);
					result.Add(action);
#else
					_actions.Add(action);
					result.Add(action);
#endif
				}
#if false
				// Add bottom separator only when wallet is not first or last item.
				if (_wallets.Count > 1 && index != _wallets.Count - 1)
				{
					var bottomSeparator = new SeparatorItemViewModel();
					_items.Insert(insertIndex + actions.Count + 1, bottomSeparator);
					result.Add(bottomSeparator);
				}
#endif
			}
		}

		private void RemoveActions(WalletViewModelBase wallet, bool dispose = false)
		{
			var actions = _walletActionsDictionary[wallet];

#if true
			_actions.Remove(wallet);
			_items.Insert(0, wallet);
#endif

			foreach (var action in actions)
			{
#if false
				_items.Remove(action);
#else
				_actions.Remove(action);
#endif
			}

			actions.Clear();

			if (dispose)
			{
				_walletActionsDictionary.Remove(wallet);
			}
		}
	}
}