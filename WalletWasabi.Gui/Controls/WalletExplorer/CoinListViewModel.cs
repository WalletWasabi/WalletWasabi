using Avalonia.Controls;
using System.Collections.Generic;
using NBitcoin;
using ReactiveUI;
using System;
using System.Linq;
using WalletWasabi.Gui.Models;
using WalletWasabi.Gui.ViewModels;
using System.ComponentModel;
using System.Collections.Specialized;
using WalletWasabi.Models;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using Avalonia.Threading;
using DynamicData;
using DynamicData.Binding;
using System.Threading.Tasks;
using System.Reactive.Disposables;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class CoinListViewModel : ViewModelBase, IDisposable
	{
		public ReadOnlyObservableCollection<CoinViewModel> Coins => _coinViewModels;
		private readonly ReadOnlyObservableCollection<CoinViewModel> _coinViewModels;
		private SourceList<CoinViewModel> _rootlist = new SourceList<CoinViewModel>();
		private SortExpressionComparer<CoinViewModel> _myComparer;

		private SortExpressionComparer<CoinViewModel> MyComparer
		{
			get => _myComparer;
			set
			{
				this.RaiseAndSetIfChanged(ref _myComparer, value);
			}
		}

		private CoinViewModel _selectedCoin;
		private bool? _selectAllCheckBoxState;
		private SortOrder _statusSortDirection;
		private SortOrder _privacySortDirection;
		private SortOrder _amountSortDirection;
		private bool? _selectPrivateCheckBoxState;
		private bool? _selectNonPrivateCheckBoxState;
		private GridLength _coinJoinStatusWidth;
		private SortOrder _historySortDirection;
		private List<CoinViewModel> _removedCoinViewModels = new List<CoinViewModel>();
		private CompositeDisposable _disposables = new CompositeDisposable();

		public ReactiveCommand EnqueueCoin { get; }
		public ReactiveCommand DequeueCoin { get; }
		public ReactiveCommand SelectAllCheckBoxCommand { get; }
		public ReactiveCommand SelectPrivateCheckBoxCommand { get; }
		public ReactiveCommand SelectNonPrivateCheckBoxCommand { get; }

		public event Action DequeueCoinsPressed;

		public event EventHandler<CoinViewModel> SelectionChanged;

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
			set
			{
				this.RaiseAndSetIfChanged(ref _statusSortDirection, value);
			}
		}

		public SortOrder AmountSortDirection
		{
			get => _amountSortDirection;
			set
			{
				this.RaiseAndSetIfChanged(ref _amountSortDirection, value);
			}
		}

		public SortOrder PrivacySortDirection
		{
			get => _privacySortDirection;
			set
			{
				this.RaiseAndSetIfChanged(ref _privacySortDirection, value);
			}
		}

		public SortOrder HistorySortDirection
		{
			get => _historySortDirection;
			set
			{
				this.RaiseAndSetIfChanged(ref _historySortDirection, value);
			}
		}

		private void RefreshOrdering()
		{
			if (AmountSortDirection != SortOrder.None)
			{
				if (AmountSortDirection == SortOrder.Increasing)
					MyComparer = SortExpressionComparer<CoinViewModel>.Ascending(cvm => cvm.Amount);
				else
					MyComparer = SortExpressionComparer<CoinViewModel>.Descending(cvm => cvm.Amount);
			}
			else if (PrivacySortDirection != SortOrder.None)
			{
				if (PrivacySortDirection == SortOrder.Increasing)
					MyComparer = SortExpressionComparer<CoinViewModel>.Ascending(cvm => cvm.AnonymitySet);
				else
					MyComparer = SortExpressionComparer<CoinViewModel>.Descending(cvm => cvm.AnonymitySet);
			}
			else if (HistorySortDirection != SortOrder.None)
			{
				if (HistorySortDirection == SortOrder.Increasing)
					MyComparer = SortExpressionComparer<CoinViewModel>.Ascending(cvm => cvm.History);
				else
					MyComparer = SortExpressionComparer<CoinViewModel>.Descending(cvm => cvm.History);
			}
			else if (StatusSortDirection != SortOrder.None)
			{
				if (StatusSortDirection == SortOrder.Increasing)
					MyComparer = SortExpressionComparer<CoinViewModel>.Ascending(cvm => cvm.Status);
				else
					MyComparer = SortExpressionComparer<CoinViewModel>.Descending(cvm => cvm.Status);
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
				if (!coin.IsSelected)
				{
					IsAllSelected = false;
					break;
				}
			bool IsAllDeselected = true;
			foreach (CoinViewModel coin in coins)
				if (coin.IsSelected)
				{
					IsAllDeselected = false;
					break;
				}
			if (IsAllDeselected) return false;
			if (IsAllSelected) return true;
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
			AmountSortDirection = SortOrder.Decreasing;
			RefreshOrdering();

			var sortChanged = this.WhenValueChanged(@this => MyComparer).Select(_ => MyComparer);

			_rootlist.Connect()
				.OnItemAdded(cvm => cvm.PropertyChanged += Coin_PropertyChanged)
				.OnItemRemoved(cvm => _removedCoinViewModels.Add(cvm)) //TODO: fix and test. If I directly unsubscribe from Coin_PropertyChanged then Unspent propchange not triggered in some cases => spent money stays in list
				.Sort(MyComparer, comparerChanged: sortChanged, resetThreshold: 5)
				.Bind(out _coinViewModels)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe().DisposeWith(_disposables);

			foreach (var sc in Global.WalletService.Coins.Where(sc => sc.Unspent))
			{
				_rootlist.Add(new CoinViewModel(sc));
			}

			Global.WalletService.Coins.CollectionChanged += Coins_CollectionGlobalChanged;

			this.WhenAnyValue(x => x.AmountSortDirection).Subscribe(x =>
			{
				if (x != SortOrder.None)
				{
					PrivacySortDirection = SortOrder.None;
					StatusSortDirection = SortOrder.None;
					HistorySortDirection = SortOrder.None;
				}
				if (x != SortOrder.None)
					RefreshOrdering();
			}).DisposeWith(_disposables);

			this.WhenAnyValue(x => x.HistorySortDirection).Subscribe(x =>
			{
				if (x != SortOrder.None)
				{
					AmountSortDirection = SortOrder.None;
					StatusSortDirection = SortOrder.None;
					PrivacySortDirection = SortOrder.None;
				}
				if (x != SortOrder.None)
					RefreshOrdering();
			}).DisposeWith(_disposables);

			this.WhenAnyValue(x => x.StatusSortDirection).Subscribe(x =>
			{
				if (x != SortOrder.None)
				{
					AmountSortDirection = SortOrder.None;
					PrivacySortDirection = SortOrder.None;
					HistorySortDirection = SortOrder.None;
				}
				if (x != SortOrder.None)
					RefreshOrdering();
			}).DisposeWith(_disposables);

			this.WhenAnyValue(x => x.PrivacySortDirection).Subscribe(x =>
			{
				if (x != SortOrder.None)
				{
					AmountSortDirection = SortOrder.None;
					StatusSortDirection = SortOrder.None;
					HistorySortDirection = SortOrder.None;
				}
				if (x != SortOrder.None)
					RefreshOrdering();
			}).DisposeWith(_disposables);

			EnqueueCoin = ReactiveCommand.Create(() =>
			{
				if (SelectedCoin == null) return;
				//await Global.ChaumianClient.QueueCoinsToMixAsync()
			}).DisposeWith(_disposables);

			DequeueCoin = ReactiveCommand.Create(() =>
			{
				if (SelectedCoin == null) return;
				DequeueCoinsPressed?.Invoke();
			}, this.WhenAnyValue(x => x.CanDeqeue)).DisposeWith(_disposables);

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
			}).DisposeWith(_disposables);

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
			}).DisposeWith(_disposables);

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
			}).DisposeWith(_disposables);
			SetSelections();
			SetCoinJoinStatusWidth();
		}

		private void Coins_CollectionGlobalChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			Dispatcher.UIThread.Post(() =>
			{
				switch (e.Action)
				{
					case NotifyCollectionChangedAction.Add:
						foreach (SmartCoin c in e.NewItems.Cast<SmartCoin>().Where(sc => sc.Unspent))
						{
							_rootlist.Add(new CoinViewModel(c));
						}
						break;

					case NotifyCollectionChangedAction.Remove:
						foreach (var c in e.OldItems.Cast<SmartCoin>())
						{
							CoinViewModel toRemove = _rootlist.Items.FirstOrDefault(cvm => cvm.Model == c);
							if (toRemove != default)
							{
								_rootlist.Remove(toRemove);
							}
						}
						break;

					case NotifyCollectionChangedAction.Reset:
						_rootlist.Clear();
						break;
				}
			});
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

		private void Coin_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			Dispatcher.UIThread.Post(() =>
			{
				if (e.PropertyName == nameof(CoinViewModel.IsSelected))
				{
					SetSelections();
					var cvm = sender as CoinViewModel;
					SelectionChanged?.Invoke(this, cvm);
				}
				if (e.PropertyName == nameof(CoinViewModel.Status))
				{
					SetCoinJoinStatusWidth();
				}
				if (e.PropertyName == nameof(CoinViewModel.Unspent))
				{
					var cvm = (CoinViewModel)sender;
					if (!cvm.Unspent)
					{
						_rootlist.Remove(cvm);
					}

					SetSelections();
					SetCoinJoinStatusWidth();
				}
			});
		}

		#region IDisposable Support

		private bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					Global.WalletService.Coins.CollectionChanged -= Coins_CollectionGlobalChanged;

					foreach (var cvm in _rootlist.Items)
					{
						cvm.PropertyChanged -= Coin_PropertyChanged;
						cvm.Dispose();
					}
					foreach (var cvm in _removedCoinViewModels)
					{
						cvm.PropertyChanged -= Coin_PropertyChanged;
						cvm.Dispose();
					}

					_rootlist.Dispose();
					_disposables.Dispose();
				}
				_rootlist = null;

				_disposedValue = true;
			}
		}

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
			// TODO: uncomment the following line if the finalizer is overridden above.
			// GC.SuppressFinalize(this);
		}

		#endregion IDisposable Support
	}
}
