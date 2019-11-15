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
using WalletWasabi.Blockchain.Transactions;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class CoinListViewModel : ViewModelBase
	{
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

		public Global Global { get; }
		public CoinListContainerType CoinListContainerType { get; }
		public ReactiveCommand<Unit, Unit> EnqueueCoin { get; }
		public ReactiveCommand<Unit, Unit> DequeueCoin { get; }
		public ReactiveCommand<Unit, Unit> SelectAllCheckBoxCommand { get; }
		public ReactiveCommand<Unit, Unit> SelectPrivateCheckBoxCommand { get; }
		public ReactiveCommand<Unit, Unit> SelectNonPrivateCheckBoxCommand { get; }
		public ReactiveCommand<Unit, Unit> SortCommand { get; }
		public ReactiveCommand<Unit, Unit> InitList { get; }

		public event EventHandler DequeueCoinsPressed;

		public event EventHandler<CoinViewModel> SelectionChanged;

		public ReadOnlyObservableCollection<CoinViewModel> Coins => _coinViewModels;

		private SortExpressionComparer<CoinViewModel> MyComparer
		{
			get => _myComparer;
			set => this.RaiseAndSetIfChanged(ref _myComparer, value);
		}

		public CoinViewModel SelectedCoin
		{
			get => _selectedCoin;
			set
			{
				this.RaiseAndSetIfChanged(ref _selectedCoin, value);
				this.RaisePropertyChanged(nameof(CanDeqeue));
			}
		}

		public bool CanDeqeue => SelectedCoin?.CoinJoinInProgress ?? false;

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
			set => this.RaiseAndSetIfChanged(backingField: ref _labelExposeCommonOwnershipWarning, value);
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

		private bool? GetCheckBoxesSelectedState(Func<CoinViewModel, bool> coinFilterPredicate)
		{
			var coins = Coins.Where(coinFilterPredicate).ToArray();

			bool isAllSelected = coins.All(coin => coin.IsSelected);
			bool isAllDeselected = coins.All(coin => !coin.IsSelected);

			if (isAllDeselected)
			{
				return false;
			}

			if (isAllSelected)
			{
				if (coins.Length != Coins.Count(coin => coin.IsSelected))
				{
					return null;
				}
				return true;
			}

			return null;
		}

		private void SelectAllCoins(bool valueOfSelected, Func<CoinViewModel, bool> coinFilterPredicate)
		{
			var coins = Coins.Where(coinFilterPredicate).ToArray();
			foreach (var c in coins)
			{
				c.IsSelected = valueOfSelected;
			}
		}

		public CoinListViewModel(Global global, CoinListContainerType coinListContainerType)
		{
			Global = global;
			CoinListContainerType = coinListContainerType;
			AmountSortDirection = SortOrder.Decreasing;
			RefreshOrdering();

			// Otherwise they're all selected as null on load.
			SelectAllCheckBoxState = false;
			SelectPrivateCheckBoxState = false;
			SelectNonPrivateCheckBoxState = false;

			var sortChanged = this.WhenValueChanged(@this => MyComparer)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Select(_ => MyComparer);

			RootList = new SourceList<CoinViewModel>();
			RootList.Connect()
				.Sort(MyComparer, comparerChanged: sortChanged, resetThreshold: 5)
				.Bind(out _coinViewModels)
				.ObserveOn(RxApp.MainThreadScheduler)
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

			EnqueueCoin = ReactiveCommand.Create(() =>
				{
					if (SelectedCoin is null)
					{
						return;
					}
					//await Global.ChaumianClient.QueueCoinsToMixAsync()
				});

			DequeueCoin = ReactiveCommand.Create(() =>
				{
					if (SelectedCoin is null)
					{
						return;
					}

					DequeueCoinsPressed?.Invoke(this, EventArgs.Empty);
				},
				this.WhenAnyValue(x => x.CanDeqeue)
					.ObserveOn(RxApp.MainThreadScheduler));

			SelectAllCheckBoxCommand = ReactiveCommand.Create(() =>
				{
					//Global.WalletService.Coins.First(c => c.Unspent).Unspent = false;
					switch (SelectAllCheckBoxState)
					{
						case true:
							SelectAllCoins(true, x => true);
							break;

						case false:
							SelectAllCoins(false, x => true);
							break;

						case null:
							SelectAllCoins(false, x => true);
							SelectAllCheckBoxState = false;
							break;
					}
				});

			SelectPrivateCheckBoxCommand = ReactiveCommand.Create(() =>
				{
					switch (SelectPrivateCheckBoxState)
					{
						case true:
							SelectAllCoins(true, x => x.AnonymitySet >= Global.Config.MixUntilAnonymitySet);
							break;

						case false:
							SelectAllCoins(false, x => x.AnonymitySet >= Global.Config.MixUntilAnonymitySet);
							break;

						case null:
							SelectAllCoins(false, x => x.AnonymitySet >= Global.Config.MixUntilAnonymitySet);
							SelectPrivateCheckBoxState = false;
							break;
					}
				});

			SelectNonPrivateCheckBoxCommand = ReactiveCommand.Create(() =>
				{
					switch (SelectNonPrivateCheckBoxState)
					{
						case true:
							SelectAllCoins(true, x => x.AnonymitySet < Global.Config.MixUntilAnonymitySet);
							break;

						case false:
							SelectAllCoins(false, x => x.AnonymitySet < Global.Config.MixUntilAnonymitySet);
							break;

						case null:
							SelectAllCoins(false, x => x.AnonymitySet < Global.Config.MixUntilAnonymitySet);
							SelectNonPrivateCheckBoxState = false;
							break;
					}
				});

			InitList = ReactiveCommand.Create(() => OnOpen(), outputScheduler: RxApp.MainThreadScheduler);

			InitList.ThrownExceptions.Subscribe(ex => Logger.LogError(ex));
		}

		private void OnOpen()
		{
			Disposables = Disposables is null ? new CompositeDisposable() : throw new NotSupportedException($"Cannot open {GetType().Name} before closing it.");

			foreach (var sc in Global.WalletService.Coins)
			{
				var newCoinVm = new CoinViewModel(this, sc);
				newCoinVm.SubscribeEvents();
				RootList.Add(newCoinVm);
			}

			Global.UiConfig.WhenAnyValue(x => x.LurkingWifeMode)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => this.RaisePropertyChanged(nameof(SelectedAmount)))
				.DisposeWith(Disposables);

			this.WhenAnyValue(x => x.SelectedAmount)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x => IsAnyCoinSelected = x is null ? false : x > Money.Zero);

			Observable.FromEventPattern<SmartCoin>(Global.WalletService.TransactionProcessor, nameof(Global.WalletService.TransactionProcessor.CoinReceived))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(coin =>
				{
					try
					{
						var newCoinVm = new CoinViewModel(this, coin.EventArgs);
						newCoinVm.SubscribeEvents();
						RootList.Add(newCoinVm);
					}
					catch (Exception ex)
					{
						Logger.LogError(ex);
					}
				})
				.DisposeWith(Disposables);

			Observable.FromEventPattern<SmartCoin>(Global.WalletService.TransactionProcessor, nameof(Global.WalletService.TransactionProcessor.CoinSpent))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(coin =>
				{
					try
					{
						CoinViewModel toRemove = RootList.Items.FirstOrDefault(cvm => cvm.Model == coin.EventArgs);
						if (toRemove != default)
						{
							toRemove.IsSelected = false;
							RootList.Remove(toRemove);
							toRemove.UnsubscribeEvents();
						}
					}
					catch (Exception ex)
					{
						Logger.LogError(ex);
					}
				})
				.DisposeWith(Disposables);

			Observable.FromEventPattern<ReplaceTransactionReceivedEventArgs>(Global.WalletService.TransactionProcessor, nameof(Global.WalletService.TransactionProcessor.ReplaceTransactionReceived))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(args =>
				{
					try
					{
						var toRemove = args.EventArgs.DestroyedCoins;
						RemoveCoins(RootList.Items.Where(cvm => toRemove.Any(sm => cvm.Model == sm)));

						var toRestore = args.EventArgs.RestoredCoins;
						AddCoins(toRestore);
					}
					catch (Exception ex)
					{
						Logger.LogError(ex);
					}
				})
				.DisposeWith(Disposables);

			Observable.FromEventPattern<DoubleSpendReceivedEventArgs>(Global.WalletService.TransactionProcessor, nameof(Global.WalletService.TransactionProcessor.DoubleSpendReceived))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(args =>
				{
					try
					{
						var toRemove = args.EventArgs.Remove;
						RemoveCoins(RootList.Items.Where(cvm => toRemove.Any(sm => cvm.Model == sm)));
					}
					catch (Exception ex)
					{
						Logger.LogError(ex);
					}
				})
				.DisposeWith(Disposables);

			Global.Config.WhenAnyValue(x => x.MixUntilAnonymitySet)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x => RefreshSelectCheckBoxesShields(x))
				.DisposeWith(Disposables);

			SetSelections();
			SetCoinJoinStatusWidth();
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
					!isStrongPrivate
					);

			SelectAllPrivateShieldState = new ShieldState(
					isCriticalPrivate,
					isSomePrivate,
					isFinePrivate,
					isStrongPrivate
					);

			SetSelections();
		}

		private void ClearRootList()
		{
			IEnumerable<CoinViewModel> toRemoves = RootList?.Items?.ToList() ?? Enumerable.Empty<CoinViewModel>(); // Clone so we can unsubscribe after clear.
			RootList?.Clear();

			foreach (CoinViewModel toRemove in toRemoves)
			{
				toRemove.UnsubscribeEvents();
			}
		}

		public void OnClose()
		{
			ClearRootList();

			Disposables?.Dispose();
			Disposables = null;
		}

		private void SetSelections()
		{
			SelectAllCheckBoxState = GetCheckBoxesSelectedState(x => true);
			SelectPrivateCheckBoxState = GetCheckBoxesSelectedState(x => x.AnonymitySet >= Global.Config.MixUntilAnonymitySet);
			SelectNonPrivateCheckBoxState = GetCheckBoxesSelectedState(x => x.AnonymitySet < Global.Config.MixUntilAnonymitySet);
		}

		private void SetCoinJoinStatusWidth()
		{
			CoinJoinStatusWidth = Coins.Any(x => x.Status == SmartCoinStatus.MixingConnectionConfirmation
				 || x.Status == SmartCoinStatus.MixingInputRegistration
				 || x.Status == SmartCoinStatus.MixingOnWaitingList
				 || x.Status == SmartCoinStatus.MixingOutputRegistration
				 || x.Status == SmartCoinStatus.MixingSigning
				 || x.Status == SmartCoinStatus.MixingWaitingForConfirmation
				 || x.Status == SmartCoinStatus.SpentAccordingToBackend)
				? new GridLength(180)
				: new GridLength(0);
		}

		public void OnCoinIsSelectedChanged(CoinViewModel cvm)
		{
			SetSelections();
			SelectionChanged?.Invoke(this, cvm);
			SelectedAmount = Coins.Where(x => x.IsSelected).Sum(x => x.Amount);
			LabelExposeCommonOwnershipWarning = CoinListContainerType == CoinListContainerType.CoinJoinTabViewModel
				? false
				: Coins.Any(c =>
					c.AnonymitySet == 1 && c.IsSelected && Coins.Any(x =>
						x.AnonymitySet > 1 && x.IsSelected));
		}

		public void OnCoinStatusChanged()
		{
			SetCoinJoinStatusWidth();
		}

		public void OnCoinUnspentChanged(CoinViewModel cvm)
		{
			if (!cvm.Unspent)
			{
				RemoveCoins(new[] { cvm });
			}
			else
			{
				SetSelections();
				SetCoinJoinStatusWidth();
			}
		}

		private void RemoveCoins(IEnumerable<CoinViewModel> cvms)
		{
			foreach (var cvm in cvms)
			{
				cvm.IsSelected = false;
				RootList.Remove(cvm);
				cvm.UnsubscribeEvents();
			}
			SetSelections();
			SetCoinJoinStatusWidth();
		}

		private void AddCoins(IEnumerable<SmartCoin> coins)
		{
			foreach (var coin in coins)
			{
				var newCoinVm = new CoinViewModel(this, coin);
				newCoinVm.SubscribeEvents();
				RootList.Add(newCoinVm);
			}
		}
	}
}
