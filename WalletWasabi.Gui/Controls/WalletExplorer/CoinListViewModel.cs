using Avalonia.Controls;
using Avalonia.Threading;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.Gui.Models;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Models;

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

		public ReactiveCommand<Unit, Unit> EnqueueCoin { get; }
		public ReactiveCommand<Unit, Unit> DequeueCoin { get; }
		public ReactiveCommand<Unit, Unit> SelectAllCheckBoxCommand { get; }
		public ReactiveCommand<Unit, Unit> SelectPrivateCheckBoxCommand { get; }
		public ReactiveCommand<Unit, Unit> SelectNonPrivateCheckBoxCommand { get; }
		public ReactiveCommand<Unit, Unit> SortCommand { get; }
		public ReactiveCommand<Unit, Unit> InitList { get; }

		public event EventHandler DequeueCoinsPressed;

		public event EventHandler<CoinViewModel> SelectionChanged;

		private List<CoinViewModel> RemovedCoinViewModels { get; }

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

		public bool CanDeqeue => SelectedCoin is null ? false : SelectedCoin.CoinJoinInProgress;

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

		private void RefreshOrdering()
		{
			if (AmountSortDirection != SortOrder.None)
			{
				if (AmountSortDirection == SortOrder.Increasing)
				{
					MyComparer = SortExpressionComparer<CoinViewModel>.Ascending(cvm => cvm.Amount);
				}
				else
				{
					MyComparer = SortExpressionComparer<CoinViewModel>.Descending(cvm => cvm.Amount);
				}
			}
			else if (PrivacySortDirection != SortOrder.None)
			{
				if (PrivacySortDirection == SortOrder.Increasing)
				{
					MyComparer = SortExpressionComparer<CoinViewModel>.Ascending(cvm => cvm.AnonymitySet);
				}
				else
				{
					MyComparer = SortExpressionComparer<CoinViewModel>.Descending(cvm => cvm.AnonymitySet);
				}
			}
			else if (ClustersSortDirection != SortOrder.None)
			{
				if (ClustersSortDirection == SortOrder.Increasing)
				{
					MyComparer = SortExpressionComparer<CoinViewModel>.Ascending(cvm => cvm.Clusters);
				}
				else
				{
					MyComparer = SortExpressionComparer<CoinViewModel>.Descending(cvm => cvm.Clusters);
				}
			}
			else if (StatusSortDirection != SortOrder.None)
			{
				if (StatusSortDirection == SortOrder.Increasing)
				{
					MyComparer = SortExpressionComparer<CoinViewModel>.Ascending(cvm => cvm.Status);
				}
				else
				{
					MyComparer = SortExpressionComparer<CoinViewModel>.Descending(cvm => cvm.Status);
				}
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

		private bool? GetCheckBoxesSelectedState(Func<CoinViewModel, bool> coinFilterPredicate)
		{
			var coins = Coins.Where(coinFilterPredicate).ToArray();
			bool IsAllSelected = true;
			foreach (CoinViewModel coin in coins)
			{
				if (!coin.IsSelected)
				{
					IsAllSelected = false;
					break;
				}
			}

			bool IsAllDeselected = true;
			foreach (CoinViewModel coin in coins)
			{
				if (coin.IsSelected)
				{
					IsAllDeselected = false;
					break;
				}
			}

			if (IsAllDeselected)
			{
				return false;
			}

			if (IsAllSelected)
			{
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

		public CoinListViewModel()
		{
			RemovedCoinViewModels = new List<CoinViewModel>();

			AmountSortDirection = SortOrder.Decreasing;
			RefreshOrdering();

			var sortChanged = this.WhenValueChanged(@this => MyComparer).Select(_ => MyComparer);

			RootList = new SourceList<CoinViewModel>();
			RootList.Connect()
				.OnItemRemoved(x => x.UnsubscribeEvents())
				.Sort(MyComparer, comparerChanged: sortChanged, resetThreshold: 5)
				.Bind(out _coinViewModels)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe();

			SortCommand = ReactiveCommand.Create(() => RefreshOrdering());

			this.WhenAnyValue(x => x.AmountSortDirection).Subscribe(x =>
			{
				if (x != SortOrder.None)
				{
					PrivacySortDirection = SortOrder.None;
					StatusSortDirection = SortOrder.None;
					ClustersSortDirection = SortOrder.None;
				}
			});

			this.WhenAnyValue(x => x.ClustersSortDirection).Subscribe(x =>
			{
				if (x != SortOrder.None)
				{
					AmountSortDirection = SortOrder.None;
					StatusSortDirection = SortOrder.None;
					PrivacySortDirection = SortOrder.None;
				}
			});

			this.WhenAnyValue(x => x.StatusSortDirection).Subscribe(x =>
			{
				if (x != SortOrder.None)
				{
					AmountSortDirection = SortOrder.None;
					PrivacySortDirection = SortOrder.None;
					ClustersSortDirection = SortOrder.None;
				}
			});

			this.WhenAnyValue(x => x.PrivacySortDirection).Subscribe(x =>
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
			}, this.WhenAnyValue(x => x.CanDeqeue));

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
						SelectAllCoins(true, x => x.AnonymitySet >= Global.Config.PrivacyLevelStrong);
						break;

					case false:
						SelectAllCoins(false, x => x.AnonymitySet >= Global.Config.PrivacyLevelStrong);
						break;

					case null:
						SelectAllCoins(false, x => x.AnonymitySet >= Global.Config.PrivacyLevelStrong);
						SelectPrivateCheckBoxState = false;
						break;
				}
			});

			SelectNonPrivateCheckBoxCommand = ReactiveCommand.Create(() =>
			{
				switch (SelectNonPrivateCheckBoxState)
				{
					case true:
						SelectAllCoins(true, x => x.AnonymitySet < Global.Config.PrivacyLevelStrong);
						break;

					case false:
						SelectAllCoins(false, x => x.AnonymitySet < Global.Config.PrivacyLevelStrong);
						break;

					case null:
						SelectAllCoins(false, x => x.AnonymitySet < Global.Config.PrivacyLevelStrong);
						SelectNonPrivateCheckBoxState = false;
						break;
				}
			});

			InitList = ReactiveCommand.Create(() =>
			{
				OnOpen();
			});
		}

		private void OnOpen()
		{
			Disposables = new CompositeDisposable();

			foreach (var sc in Global.WalletService.Coins.Where(sc => sc.Unspent))
			{
				var newCoinVm = new CoinViewModel(this, sc);
				newCoinVm.SubscribeEvents();
				RootList.Add(newCoinVm);
			}

			Observable.FromEventPattern<NotifyCollectionChangedEventArgs>(Global.WalletService.Coins, nameof(Global.WalletService.Coins.CollectionChanged))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x =>
				{
					var e = x.EventArgs;
					try
					{
						switch (e.Action)
						{
							case NotifyCollectionChangedAction.Add:
								foreach (SmartCoin c in e.NewItems.Cast<SmartCoin>().Where(sc => sc.Unspent && !sc.IsDust))
								{
									var newCoinVm = new CoinViewModel(this, c);
									newCoinVm.SubscribeEvents();
									RootList.Add(newCoinVm);
								}
								break;

							case NotifyCollectionChangedAction.Remove:
								foreach (var c in e.OldItems.Cast<SmartCoin>())
								{
									CoinViewModel toRemove = RootList.Items.FirstOrDefault(cvm => cvm.Model == c);
									if (toRemove != default)
									{
										RootList.Remove(toRemove);
									}
								}
								break;

							case NotifyCollectionChangedAction.Reset:
								ClearRootList();
								break;
						}
					}
					catch (Exception ex)
					{
						Logging.Logger.LogDebug<Dispatcher>(ex);
					}
				}).DisposeWith(Disposables);

			SetSelections();
			SetCoinJoinStatusWidth();
		}

		private void ClearRootList() => RootList.Clear();

		public void OnClose()
		{
			ClearRootList();

			Disposables?.Dispose();
		}

		private void SetSelections()
		{
			SelectAllCheckBoxState = GetCheckBoxesSelectedState(x => true);
			SelectPrivateCheckBoxState = GetCheckBoxesSelectedState(x => x.AnonymitySet >= Global.Config.PrivacyLevelStrong);
			SelectNonPrivateCheckBoxState = GetCheckBoxesSelectedState(x => x.AnonymitySet < Global.Config.PrivacyLevelStrong);
		}

		private void SetCoinJoinStatusWidth()
		{
			if (Coins.Any(x => x.Status == SmartCoinStatus.MixingConnectionConfirmation
				 || x.Status == SmartCoinStatus.MixingInputRegistration
				 || x.Status == SmartCoinStatus.MixingOnWaitingList
				 || x.Status == SmartCoinStatus.MixingOutputRegistration
				 || x.Status == SmartCoinStatus.MixingSigning
				 || x.Status == SmartCoinStatus.MixingWaitingForConfirmation
				 || x.Status == SmartCoinStatus.SpentAccordingToBackend))
			{
				CoinJoinStatusWidth = new GridLength(180);
			}
			else
			{
				CoinJoinStatusWidth = new GridLength(0);
			}
		}

		public void OnCoinIsSelectedChanged(CoinViewModel cvm)
		{
			SetSelections();
			SelectionChanged?.Invoke(this, cvm);
		}

		public void OnCoinStatusChanged()
		{
			SetCoinJoinStatusWidth();
		}

		public void OnCoinUnspentChanged(CoinViewModel cvm)
		{
			if (!cvm.Unspent)
			{
				Dispatcher.UIThread.Post(() =>
				{
					RootList.Remove(cvm);
				});
			}

			SetSelections();
			SetCoinJoinStatusWidth();
		}
	}
}
