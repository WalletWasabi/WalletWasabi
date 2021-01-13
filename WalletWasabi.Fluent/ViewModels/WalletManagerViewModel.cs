using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using Avalonia.Threading;
using DynamicData;
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
		private readonly Dictionary<WalletViewModelBase, List<WalletActionViewModel>> _walletActionsDictionary;
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
			_walletActionsDictionary = new Dictionary<WalletViewModelBase, List<WalletActionViewModel>>();
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
				if (SelectedWallet is not null)
				{
					_items.Insert(index - 1, wallet);
				}
				else
				{
					_items.Insert(index, wallet);
				}
			}

			_walletDictionary.Add(wallet.Wallet, wallet);
		}

		private void RemoveWallet(WalletViewModelBase walletViewModel)
		{
			var isLoggedIn = walletViewModel.Wallet.IsLoggedIn;

			walletViewModel.Dispose();

			_wallets.Remove(walletViewModel);
			_items.Remove(walletViewModel);

			if (isLoggedIn)
			{
				var actions = _walletActionsDictionary[walletViewModel];

				RemoveActions(walletViewModel, actions, true);

				_walletActionsDictionary.Remove(walletViewModel);
			}

			_walletDictionary.Remove(walletViewModel.Wallet);
		}

		private void LoadWallets(WalletManager walletManager)
		{
			foreach (var wallet in walletManager.GetWallets())
			{
				InsertWallet(ClosedWalletViewModel.Create(walletManager, wallet));
			}
		}

		public NavBarItemViewModel? SelectionChanged(NavBarItemViewModel item)
		{
			var result = default(NavBarItemViewModel);

			if (SelectedWallet == item)
			{
				return result;
			}

			if (SelectedWallet is { IsLoggedIn: true } walletViewModelPrevious && item is WalletViewModelBase { IsLoggedIn: true })
			{
				if (item is not WalletActionViewModel && SelectedWallet != item)
				{
					var actions = _walletActionsDictionary[walletViewModelPrevious];

					RemoveActions(walletViewModelPrevious, actions);

					SelectedWallet = null;

					result = item;
				}
			}

			if (item is WalletViewModelBase { IsLoggedIn: true} walletViewModelItem)
			{
				if (!_walletActionsDictionary.TryGetValue(walletViewModelItem, out var actions))
				{
					actions = GetWalletActions(walletViewModelItem);
					_walletActionsDictionary[walletViewModelItem] = actions;
				}

				InsertActions(walletViewModelItem, actions);

				SelectedWallet = walletViewModelItem;

				result = item;
			}

			return result;
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

		private void InsertActions(WalletViewModelBase walletViewModel, IEnumerable<NavBarItemViewModel> actions)
		{
			_items.Remove(walletViewModel);
			// _actions.Add(walletViewModel);

			// foreach (var action in actions)
			// {
			// 	_actions.Add(action);
			// }

			_actions.AddRange(actions.ToList().Prepend(walletViewModel));
		}

		private void RemoveActions(WalletViewModelBase walletViewModel, IEnumerable<NavBarItemViewModel> actions, bool dispose = false)
		{
			// _actions.Remove(walletViewModel);

			_actions.Clear();

			var index = _wallets.IndexOf(walletViewModel);
			if (index >= 0)
			{
				_items.Insert(index, walletViewModel);
			}

			// foreach (var action in actions)
			// {
			// 	_actions.Remove(action);
			// }

			if (dispose)
			{
				_walletActionsDictionary.Remove(walletViewModel);
			}
		}
	}
}