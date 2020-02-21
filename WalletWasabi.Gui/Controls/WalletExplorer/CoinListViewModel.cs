using Avalonia.Controls;
using Avalonia.Threading;
using DynamicData;
using DynamicData.Binding;
using NBitcoin;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using WalletWasabi.Gui.Models;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Logging;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.TransactionProcessing;
using Splat;
using WalletWasabi.Services;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class CoinListViewModel : ViewModelBase
	{
		private static HashSet<SmartCoinStatus> NotVisibleStatuses = new HashSet<SmartCoinStatus>()
		{
			SmartCoinStatus.Confirmed,
			SmartCoinStatus.Unconfirmed
		};

		private CompositeDisposable Disposables { get; set; }

		public SourceList<CoinViewModel> RootList { get; private set; }
		
		private ReadOnlyObservableCollection<CoinViewModel> _coinViewModels;
		private SortExpressionComparer<CoinViewModel> _myComparer;

		private CoinViewModel _selectedCoin;
		private bool? _selectAllCheckBoxState;
		private SortOrder _statusSortDirection;
		private SortOrder _privacySortDirection;
		private SortOrder _amountSortDirection;
		private bool? _selectPrivateCheckBoxState;
		private bool? _selectNonPrivateCheckBoxState;
		private GridLength _coinJoinStatusWidth;
		private SortOrder _clustersSortDirection;
		private Money _selectedAmount;
		private bool _isAnyCoinSelected;
		private bool _labelExposeCommonOwnershipWarning;
		private bool _selectAllNonPrivateVisible;
		private bool _selectAllPrivateVisible;
		private ShieldState _selectAllPrivateShieldState;
		private ShieldState _selectAllNonPrivateShieldState;
		private bool _isCoinListLoading;
		private object SelectionChangedLock { get; } = new object();
		private object StateChangedLock { get; } = new object();
		private Global Global { get; }
		private WalletService WalletService { get; }
		public ReactiveCommand<Unit, Unit> SelectAllCheckBoxCommand { get; }
		public ReactiveCommand<Unit, Unit> SelectPrivateCheckBoxCommand { get; }
		public ReactiveCommand<Unit, Unit> SelectNonPrivateCheckBoxCommand { get; }
		public ReactiveCommand<Unit, Unit> SortCommand { get; }
		public ReactiveCommand<Unit, Unit> InitList { get; }

		public event EventHandler<SmartCoin> DequeueCoinsPressed;

		public event EventHandler CoinListShown;

		public event EventHandler SelectionChanged;

		public ReadOnlyObservableCollection<CoinViewModel> Coins => _coinViewModels;

		public bool DisplayCommonOwnershipWarning { get; set; } = false;

		public bool CanDequeueCoins { get; set; } = false;

		private SortExpressionComparer<CoinViewModel> MyComparer
		{
			get => _myComparer;
			set => this.RaiseAndSetIfChanged(ref _myComparer, value);
		}

		public CoinViewModel SelectedCoin
		{
			get => _selectedCoin;
			set => this.RaiseAndSetIfChanged(ref _selectedCoin, value);
		}

		public bool? SelectAllCheckBoxState
		{
			get => _selectAllCheckBoxState;
			set => this.RaiseAndSetIfChanged(ref _selectAllCheckBoxState, value);
		}

		public bool? SelectPrivateCheckBoxState
		{
			get => _selectPrivateCheckBoxState;
			set => this.RaiseAndSetIfChanged(ref _selectPrivateCheckBoxState, value);
		}

		public SortOrder StatusSortDirection
		{
			get => _statusSortDirection;
			set => this.RaiseAndSetIfChanged(ref _statusSortDirection, value);
		}

		public void PressDequeue(SmartCoin coin)
		{
			DequeueCoinsPressed?.Invoke(this, coin);
		}

		public SortOrder AmountSortDirection
		{
			get => _amountSortDirection;
			set => this.RaiseAndSetIfChanged(ref _amountSortDirection, value);
		}

		public SortOrder PrivacySortDirection
		{
			get => _privacySortDirection;
			set => this.RaiseAndSetIfChanged(ref _privacySortDirection, value);
		}

		public SortOrder ClustersSortDirection
		{
			get => _clustersSortDirection;
			set => this.RaiseAndSetIfChanged(ref _clustersSortDirection, value);
		}

		public Money SelectedAmount
		{
			get => _selectedAmount;
			set => this.RaiseAndSetIfChanged(ref _selectedAmount, value);
		}

		public bool IsAnyCoinSelected
		{
			get => _isAnyCoinSelected;
			set => this.RaiseAndSetIfChanged(ref _isAnyCoinSelected, value);
		}

		public bool LabelExposeCommonOwnershipWarning
		{
			get => _labelExposeCommonOwnershipWarning;
			set => this.RaiseAndSetIfChanged(ref _labelExposeCommonOwnershipWarning, value);
		}

		private void RefreshOrdering()
		{
			var sortExpression = new SortExpressionComparer<CoinViewModel>();
			if (AmountSortDirection != SortOrder.None)
			{
				MyComparer = AmountSortDirection == SortOrder.Increasing
					? sortExpression.ThenByAscending(cvm => cvm.Amount)
					: sortExpression.ThenByDescending(cvm => cvm.Amount);
			}
			else if (PrivacySortDirection != SortOrder.None)
			{
				MyComparer = PrivacySortDirection == SortOrder.Increasing
					? sortExpression.ThenByAscending(cvm => cvm.AnonymitySet)
					: sortExpression.ThenByDescending(cvm => cvm.AnonymitySet);
			}
			else if (ClustersSortDirection != SortOrder.None)
			{
				MyComparer = ClustersSortDirection == SortOrder.Increasing
					? sortExpression.ThenByAscending(cvm => cvm.Clusters)
					: sortExpression.ThenByDescending(cvm => cvm.Clusters);
			}
			else if (StatusSortDirection != SortOrder.None)
			{
				MyComparer = StatusSortDirection == SortOrder.Increasing
					? sortExpression.ThenByAscending(cvm => cvm.Status)
					: sortExpression.ThenByDescending(cvm => cvm.Status);
			}
		}

		public bool? SelectNonPrivateCheckBoxState
		{
			get => _selectNonPrivateCheckBoxState;
			set => this.RaiseAndSetIfChanged(ref _selectNonPrivateCheckBoxState, value);
		}

		public GridLength CoinJoinStatusWidth
		{
			get => _coinJoinStatusWidth;
			set => this.RaiseAndSetIfChanged(ref _coinJoinStatusWidth, value);
		}

		public bool SelectAllNonPrivateVisible
		{
			get => _selectAllNonPrivateVisible;
			set => this.RaiseAndSetIfChanged(ref _selectAllNonPrivateVisible, value);
		}

		public bool SelectAllPrivateVisible
		{
			get => _selectAllPrivateVisible;
			set => this.RaiseAndSetIfChanged(ref _selectAllPrivateVisible, value);
		}

		public ShieldState SelectAllPrivateShieldState
		{
			get => _selectAllPrivateShieldState;
			set => this.RaiseAndSetIfChanged(ref _selectAllPrivateShieldState, value);
		}

		public ShieldState SelectAllNonPrivateShieldState
		{
			get => _selectAllNonPrivateShieldState;
			set => this.RaiseAndSetIfChanged(ref _selectAllNonPrivateShieldState, value);
		}

		public bool IsCoinListLoading
		{
			get => _isCoinListLoading;
			set => this.RaiseAndSetIfChanged(ref _isCoinListLoading, value);
		}

		private bool? GetCheckBoxesSelectedState(CoinViewModel[] allCoins, Func<CoinViewModel, bool> coinFilterPredicate)
		{
			var coins = allCoins.Where(coinFilterPredicate).ToArray();

			bool isAllSelected = coins.All(coin => coin.IsSelected);
			bool isAllDeselected = coins.All(coin => !coin.IsSelected);

			if (isAllDeselected)
			{
				return false;
			}

			if (isAllSelected)
			{
				if (coins.Length != allCoins.Count(coin => coin.IsSelected))
				{
					return null;
				}
				return true;
			}

			return null;
		}

		private void SelectCoins(Func<CoinViewModel, bool> coinFilterPredicate)
		{
			foreach (var c in Coins.ToArray())
			{
				c.IsSelected = coinFilterPredicate(c);
			}
		}

		public CoinListViewModel(WalletService walletService, bool canDequeueCoins = false, bool displayCommonOwnershipWarning = false)
		{
			Global = Locator.Current.GetService<Global>();
			WalletService = walletService;

			AmountSortDirection = SortOrder.Decreasing;

			CoinJoinStatusWidth = new GridLength(0);			
			CanDequeueCoins = canDequeueCoins;
			DisplayCommonOwnershipWarning = displayCommonOwnershipWarning;

			RefreshOrdering();

			// Otherwise they're all selected as null on load.
			SelectAllCheckBoxState = false;
			SelectPrivateCheckBoxState = false;
			SelectNonPrivateCheckBoxState = false;

			var sortChanged = this
				.WhenAnyValue(x => x.MyComparer)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Select(_ => MyComparer);

			RootList = new SourceList<CoinViewModel>();
			RootList
				.Connect()
				.Sort(MyComparer, comparerChanged: sortChanged, resetThreshold: 5)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Bind(out _coinViewModels)
				.Subscribe();

			SortCommand = ReactiveCommand.Create(RefreshOrdering);

			this.WhenAnyValue(x => x.AmountSortDirection)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x =>
				{
					if (x != SortOrder.None)
					{
						PrivacySortDirection = SortOrder.None;
						StatusSortDirection = SortOrder.None;
						ClustersSortDirection = SortOrder.None;
					}
				});

			this.WhenAnyValue(x => x.ClustersSortDirection)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x =>
				{
					if (x != SortOrder.None)
					{
						AmountSortDirection = SortOrder.None;
						StatusSortDirection = SortOrder.None;
						PrivacySortDirection = SortOrder.None;
					}
				});

			this.WhenAnyValue(x => x.StatusSortDirection)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x =>
				{
					if (x != SortOrder.None)
					{
						AmountSortDirection = SortOrder.None;
						PrivacySortDirection = SortOrder.None;
						ClustersSortDirection = SortOrder.None;
					}
				});

			this.WhenAnyValue(x => x.PrivacySortDirection)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x =>
				{
					if (x != SortOrder.None)
					{
						AmountSortDirection = SortOrder.None;
						StatusSortDirection = SortOrder.None;
						ClustersSortDirection = SortOrder.None;
					}
				});

			SelectAllCheckBoxCommand = ReactiveCommand.Create(() =>
				{
					switch (SelectAllCheckBoxState)
					{
						case true:
							SelectCoins(x => true);
							break;

						case null:
						case false:
							SelectCoins(x => false);
							SelectAllCheckBoxState = false;
							break;
					}
				});

			SelectPrivateCheckBoxCommand = ReactiveCommand.Create(() =>
				{
					switch (SelectPrivateCheckBoxState)
					{
						case true:
							SelectCoins(x => x.AnonymitySet >= Global.Config.MixUntilAnonymitySet);
							break;

						case null:
						case false:
							SelectCoins(x => false);
							SelectPrivateCheckBoxState = false;
							break;
					}
				});

			SelectNonPrivateCheckBoxCommand = ReactiveCommand.Create(() =>
				{
					switch (SelectNonPrivateCheckBoxState)
					{
						case true:
							SelectCoins(x => x.AnonymitySet < Global.Config.MixUntilAnonymitySet);
							break;

						case false:
						case null:
							SelectCoins(x => false);
							SelectNonPrivateCheckBoxState = false;
							break;
					}
				});

			// This will be triggered after the Tab becomes visible for the user.
			InitList = ReactiveCommand.CreateFromTask(async () =>
			{
				IsCoinListLoading = true; // Set the Loading animation.
				await Task.Delay(800); // Let the UI to be rendered to the user.
				OnOpen();
				CoinListShown?.Invoke(this, null); // Trigger this event to refresh the list.
			});

			Observable
				.Merge(InitList.ThrownExceptions)
				.Merge(SelectNonPrivateCheckBoxCommand.ThrownExceptions)
				.Merge(SelectPrivateCheckBoxCommand.ThrownExceptions)
				.Merge(SelectAllCheckBoxCommand.ThrownExceptions)
				.Merge(SortCommand.ThrownExceptions)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex => Logger.LogError(ex));
		}

		private void RefreshSelectionCheckBoxes(CoinViewModel[] coins)
		{
			SelectAllCheckBoxState = GetCheckBoxesSelectedState(coins, x => true);
			SelectPrivateCheckBoxState = GetCheckBoxesSelectedState(coins, x => x.AnonymitySet >= Global.Config.MixUntilAnonymitySet);
			SelectNonPrivateCheckBoxState = GetCheckBoxesSelectedState(coins, x => x.AnonymitySet < Global.Config.MixUntilAnonymitySet);
		}

		private void RefreshStatusColumnWidth(CoinViewModel[] coins)
		{
			CoinJoinStatusWidth = coins.Any() && coins.All(x => NotVisibleStatuses.Contains(x.Status))
										 ? new GridLength(0)
										 : new GridLength(180);
		}

		private void OnOpen()
		{
			Disposables = Disposables is null ?
				new CompositeDisposable() :
				throw new NotSupportedException($"Cannot open {GetType().Name} before closing it.");

			Global.UiConfig
				.WhenAnyValue(x => x.LurkingWifeMode)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => this.RaisePropertyChanged(nameof(SelectedAmount)))
				.DisposeWith(Disposables);

			Observable
				.Merge(Observable.FromEventPattern<ProcessedResult>(WalletService.TransactionProcessor, nameof(WalletService.TransactionProcessor.WalletRelevantTransactionProcessed)).Select(_ => Unit.Default))
				.Throttle(TimeSpan.FromSeconds(1)) // Throttle TransactionProcessor events adds/removes.
				.Merge(Observable.FromEventPattern(this, nameof(CoinListShown), RxApp.MainThreadScheduler).Select(_ => Unit.Default)) // Load the list immediately.
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(args =>
				{
					try
					{
						var actual = WalletService.TransactionProcessor.Coins.ToHashSet();
						var old = RootList.Items.ToDictionary(c => c.Model, c => c);

						var coinToRemove = old.Where(c => !actual.Contains(c.Key)).ToArray();
						var coinToAdd = actual.Where(c => !old.ContainsKey(c)).ToArray();

						RootList.RemoveMany(coinToRemove.Select(kp => kp.Value));

						var newCoinViewModels = coinToAdd.Select(c => new CoinViewModel(WalletService, this, c)).ToArray();
						foreach (var cvm in newCoinViewModels)
						{
							SubscribeToCoinEvents(cvm);
						}
						RootList.AddRange(newCoinViewModels);

						var allCoins = RootList.Items.ToArray();

						RefreshSelectionCheckBoxes(allCoins);
						RefreshStatusColumnWidth(allCoins);

						foreach (var item in coinToRemove)
						{
							item.Value.Dispose();
						}
					}
					catch (Exception ex)
					{
						Logger.LogError(ex);
					}
					finally
					{
						IsCoinListLoading = false;
					}
				})
				.DisposeWith(Disposables);

			Global.Config
				.WhenAnyValue(x => x.MixUntilAnonymitySet)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x =>
				{
					try
					{
						RefreshSelectCheckBoxesShields(x);
						RefreshSelectionCheckBoxes(RootList.Items.ToArray());
					}
					catch (Exception ex)
					{
						Logger.LogError(ex);
					}
				})
				.DisposeWith(Disposables);
		}

		private void SubscribeToCoinEvents(CoinViewModel cvm)
		{
			cvm.WhenAnyValue(x => x.IsSelected)
				.Synchronize(SelectionChangedLock) // Use the same lock to ensure thread safety.
				.Throttle(TimeSpan.FromMilliseconds(100))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x =>
				{
					try
					{
						var coins = RootList.Items.ToArray();

						RefreshSelectionCheckBoxes(coins);
						var selectedCoins = coins.Where(x => x.IsSelected).ToArray();

						SelectedAmount = selectedCoins.Sum(x => x.Amount);
						IsAnyCoinSelected = selectedCoins.Any();

						LabelExposeCommonOwnershipWarning = !DisplayCommonOwnershipWarning
							? false
							: selectedCoins
								.Where(c => c.AnonymitySet == 1)
								.Any(x => selectedCoins.Any(x => x.AnonymitySet > 1));

						SelectionChanged?.Invoke(this, null);
					}
					catch (Exception ex)
					{
						Logger.LogError(ex);
					}
				})
				.DisposeWith(cvm.GetDisposables()); // Subscription will be disposed with the coinViewModel.

			Observable
				.Merge(cvm.Model.WhenAnyValue(x => x.IsBanned, x => x.SpentAccordingToBackend, x => x.Confirmed, x => x.CoinJoinInProgress).Select(_ => Unit.Default))
				.Merge(Observable.FromEventPattern(WalletService.ChaumianClient, nameof(WalletService.ChaumianClient.StateUpdated)).Select(_ => Unit.Default))
				.Synchronize(StateChangedLock) // Use the same lock to ensure thread safety.
				.Throttle(TimeSpan.FromSeconds(1))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x =>
				{
					try
					{
						RefreshStatusColumnWidth(RootList.Items.ToArray());
					}
					catch (Exception ex)
					{
						Logger.LogError(ex);
					}
				})
				.DisposeWith(cvm.GetDisposables()); // Subscription will be disposed with the coinViewModel.
		}

		private void RefreshSelectCheckBoxesShields(int mixUntilAnonymitySet)
		{
			var isCriticalPrivate = false;
			var isSomePrivate = mixUntilAnonymitySet <= Global.Config.PrivacyLevelSome;
			var isFinePrivate = mixUntilAnonymitySet <= Global.Config.PrivacyLevelFine;
			var isStrongPrivate = mixUntilAnonymitySet <= Global.Config.PrivacyLevelStrong;

			SelectAllNonPrivateShieldState = new ShieldState(
					!isCriticalPrivate,
					!isSomePrivate,
					!isFinePrivate,
					!isStrongPrivate);

			SelectAllPrivateShieldState = new ShieldState(
					isCriticalPrivate,
					isSomePrivate,
					isFinePrivate,
					isStrongPrivate);
		}

		public void OnClose()
		{
			foreach (var cvm in RootList.Items)
			{
				cvm.Dispose();
			}

			RootList.Clear();

			// Do not dispose the RootList here. It will be reused next time when you open CoinJoinTab or SendTab.
			Disposables?.Dispose();
			Disposables = null;
		}
	}
}
