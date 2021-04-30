using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Actions;
using WalletWasabi.Fluent.ViewModels.Wallets.Receive;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;
using WalletWasabi.Gui;
using WalletWasabi.Logging;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Fluent.ViewModels
{
	public partial class WalletManagerViewModel : ViewModelBase
	{
		private readonly Dictionary<Wallet, WalletViewModelBase> _walletDictionary;
		private readonly Dictionary<WalletViewModelBase, List<NavBarItemViewModel>> _walletActionsDictionary;
		private readonly ReadOnlyObservableCollection<NavBarItemViewModel> _items;
		private readonly Config _config;
		private readonly UiConfig _uiConfig;
		private readonly TransactionBroadcaster _transactionBroadcaster;
		private readonly HttpClientFactory _externalHttpClientFactory;
		private NavBarItemViewModel? _currentSelection;
		[AutoNotify] private WalletViewModelBase? _selectedWallet;
		[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _isLoadingWallet;
		[AutoNotify] private bool _loggedInAndSelectedAlwaysFirst;
		[AutoNotify] private ObservableCollection<NavBarItemViewModel> _actions;
		[AutoNotify] private ObservableCollection<NavBarItemViewModel> _selectedWallets;
		[AutoNotify] private ObservableCollection<WalletViewModelBase> _wallets;
		[AutoNotify] private bool _anyWalletStarted;

		public WalletManagerViewModel(WalletManager walletManager, UiConfig uiConfig, Config config, BitcoinStore bitcoinStore,
			LegalChecker legalChecker, TransactionBroadcaster broadcaster, HttpClientFactory externalHttpClientFactory)
		{
			WalletManager = walletManager;
			BitcoinStore = bitcoinStore;
			LegalChecker = legalChecker;
			_walletDictionary = new Dictionary<Wallet, WalletViewModelBase>();
			_walletActionsDictionary = new Dictionary<WalletViewModelBase, List<NavBarItemViewModel>>();
			_actions = new ObservableCollection<NavBarItemViewModel>();
			_selectedWallets = new ObservableCollection<NavBarItemViewModel>();
			_wallets = new ObservableCollection<WalletViewModelBase>();
			_loggedInAndSelectedAlwaysFirst = true;
			_config = config;
			_uiConfig = uiConfig;
			_transactionBroadcaster = broadcaster;
			_externalHttpClientFactory = externalHttpClientFactory;

			static Func<WalletViewModelBase, bool> SelectedWalletFilter(WalletViewModelBase? selected)
			{
				return item => selected is null || item != selected;
			}

			var selectedWalletFilter = this.WhenValueChanged(t => t.SelectedWallet).Select(SelectedWalletFilter);

			_wallets
				.ToObservableChangeSet()
				.Filter(selectedWalletFilter)
				.Sort(SortExpressionComparer<WalletViewModelBase>.Descending(i => i.WalletState).ThenByDescending(i => i.IsLoggedIn).ThenByAscending(i => i.Title))
				.Transform(x => x as NavBarItemViewModel)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Bind(out _items)
				.AsObservableList();

			Observable
				.FromEventPattern<WalletState>(walletManager, nameof(WalletWasabi.Wallets.WalletManager.WalletStateChanged))
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
						else if (_walletDictionary[wallet] is ClosedWalletViewModel { IsLoggedIn: true } cwvm && wallet.State == WalletState.Started)
						{
							OpenClosedWallet(uiConfig, cwvm);
						}
					}

					AnyWalletStarted = Items.OfType<WalletViewModelBase>().Any(y => y.WalletState == WalletState.Started);
				});

			Observable
				.FromEventPattern<Wallet>(walletManager, nameof(WalletWasabi.Wallets.WalletManager.WalletAdded))
				.Select(x => x.EventArgs)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(
					wallet =>
				{
					WalletViewModelBase vm = (wallet.State <= WalletState.Starting)
						? ClosedWalletViewModel.Create(this, wallet)
						: WalletViewModel.Create(uiConfig, wallet);

					InsertWallet(vm);
				});

			RxApp.MainThreadScheduler.Schedule(() => EnumerateWallets(walletManager));
		}

		public ReadOnlyObservableCollection<NavBarItemViewModel> Items => _items;

		public WalletManager WalletManager { get; }

		public BitcoinStore BitcoinStore { get; }

		public LegalChecker LegalChecker { get; }

		public async Task LoadWalletAsync(ClosedWalletViewModel closedWalletViewModel)
		{
			var wallet = closedWalletViewModel.Wallet;

			if (wallet.State != WalletState.Uninitialized)
			{
				throw new Exception("Wallet is already being logged in.");
			}

			try
			{
				await Task.Run(async () => await WalletManager.StartWalletAsync(wallet));
			}
			catch (OperationCanceledException ex)
			{
				Logger.LogTrace(ex);
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
			}
		}

		private void OpenClosedWallet(UiConfig uiConfig, ClosedWalletViewModel closedWalletViewModel)
		{
			IsLoadingWallet = true;

			RemoveWallet(closedWalletViewModel);

			var walletViewModelItem = OpenWallet(uiConfig, closedWalletViewModel.Wallet);

			if (!_walletActionsDictionary.TryGetValue(walletViewModelItem, out var actions))
			{
				actions = GetWalletActions(walletViewModelItem);
				_walletActionsDictionary[walletViewModelItem] = actions;
			}

			if (_currentSelection == closedWalletViewModel)
			{
				InsertActions(walletViewModelItem, actions);
				SelectedWallet = walletViewModelItem;
			}

			IsLoadingWallet = false;
		}

		private WalletViewModel OpenWallet(UiConfig uiConfig, Wallet wallet)
		{
			if (_wallets.Any(x => x.Title == wallet.WalletName))
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

			_walletDictionary.Add(wallet.Wallet, wallet);
		}

		private void RemoveWallet(WalletViewModelBase walletViewModel)
		{
			var isLoggedIn = walletViewModel.Wallet.IsLoggedIn;

			_wallets.Remove(walletViewModel);

			if (isLoggedIn)
			{
				if (_walletActionsDictionary.ContainsKey(walletViewModel))
				{
					var actions = _walletActionsDictionary[walletViewModel];

					RemoveActions(walletViewModel, actions, true);

					_walletActionsDictionary.Remove(walletViewModel);
				}
			}

			_walletDictionary.Remove(walletViewModel.Wallet);
		}

		private void EnumerateWallets(WalletManager walletManager)
		{
			foreach (var wallet in walletManager.GetWallets())
			{
				InsertWallet(ClosedWalletViewModel.Create(this, wallet));
			}
		}

		public NavBarItemViewModel? SelectionChanged(NavBarItemViewModel item)
		{
			if (item.SelectionMode == NavBarItemSelectionMode.Selected)
			{
				_currentSelection = item;
			}

			if (IsLoadingWallet || SelectedWallet == item)
			{
				return default;
			}

			var result = default(NavBarItemViewModel);

			if (SelectedWallet is { IsLoggedIn: true } walletViewModelPrevious && (item is WalletViewModelBase && SelectedWallet != item))
			{
				if (/*item is not WalletActionViewModel &&*/ SelectedWallet != item)
				{
					var actions = _walletActionsDictionary[walletViewModelPrevious];

					RemoveActions(walletViewModelPrevious, actions);

					SelectedWallet = null;

					result = item;
				}
			}

			if (item is WalletViewModel { IsLoggedIn: true } walletViewModelItem)
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

		private List<NavBarItemViewModel> GetWalletActions(WalletViewModel walletViewModel)
		{
			var wallet = walletViewModel.Wallet;
			var actions = new List<NavBarItemViewModel>();

			if (wallet.KeyManager.IsHardwareWallet || !wallet.KeyManager.IsWatchOnly)
			{
				actions.Add(new SendViewModel(walletViewModel, _transactionBroadcaster, _config, _uiConfig, _externalHttpClientFactory));
			}

			actions.Add(new ReceiveViewModel(walletViewModel, WalletManager, BitcoinStore));

			if (!wallet.KeyManager.IsWatchOnly)
			{
				actions.Add(new CoinJoinWalletActionViewModel(walletViewModel));
			}

			actions.Add(new AdvancedWalletActionViewModel(walletViewModel));

			return actions;
		}

		private void InsertActions(WalletViewModelBase walletViewModel, IEnumerable<NavBarItemViewModel> actions)
		{
			// _actions.AddRange(actions.ToList().Prepend(walletViewModel));
			_actions.AddRange(actions.ToList());
			_selectedWallets.Add(walletViewModel);
		}

		private void RemoveActions(WalletViewModelBase walletViewModel, IEnumerable<NavBarItemViewModel> actions, bool dispose = false)
		{
			_actions.Clear();
			_selectedWallets.Clear();

			if (dispose)
			{
				_walletActionsDictionary.Remove(walletViewModel);
			}
		}
	}
}
