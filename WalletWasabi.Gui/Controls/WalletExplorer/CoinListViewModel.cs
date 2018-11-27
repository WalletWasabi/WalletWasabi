using Avalonia.Controls;
using System.Collections.Generic;
using NBitcoin;
using ReactiveUI;
using ReactiveUI.Legacy;
using System;
using System.Linq;
using WalletWasabi.Gui.Models;
using WalletWasabi.Gui.ViewModels;
using System.ComponentModel;
using System.Collections.Specialized;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class CoinListViewModel : ViewModelBase
	{
#pragma warning disable CS0618 // Type or member is obsolete
		private IReactiveDerivedList<CoinViewModel> _coins;
		private IReactiveDerivedList<CoinViewModel> _rootcoinlist;
		private CoinViewModel _selectedCoin;
		private bool? _selectAllCheckBoxState;
		private SortOrder _statusSortDirection;
		private SortOrder _privacySortDirection;
		private SortOrder _amountSortDirection;
		private bool? _selectPrivateCheckBoxState;
		private bool? _selectNonPrivateCheckBoxState;
		private GridLength _coinJoinStatusWidth;
		private SortOrder _historySortDirection;

		public ReactiveCommand EnqueueCoin { get; }
		public ReactiveCommand DequeueCoin { get; }
		public ReactiveCommand SelectAllCheckBoxCommand { get; }
		public ReactiveCommand SelectPrivateCheckBoxCommand { get; }
		public ReactiveCommand SelectNonPrivateCheckBoxCommand { get; }

		public event Action DequeueCoinsPressed;

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
				if (value != SortOrder.None)
				{
					AmountSortDirection = SortOrder.None;
					PrivacySortDirection = SortOrder.None;
					HistorySortDirection = SortOrder.None;
				}

				switch (value)
				{
					case SortOrder.Increasing:
						Coins = _rootcoinlist.CreateDerivedCollection(x => x, x => true, (x, y) => x.Status.CompareTo(y.Status));
						break;

					case SortOrder.Decreasing:
						Coins = _rootcoinlist.CreateDerivedCollection(x => x, x => true, (x, y) => y.Status.CompareTo(x.Status));
						break;
				}
			}
		}

		public SortOrder AmountSortDirection
		{
			get => _amountSortDirection;
			set
			{
				this.RaiseAndSetIfChanged(ref _amountSortDirection, value);
				if (value != SortOrder.None)
				{
					PrivacySortDirection = SortOrder.None;
					StatusSortDirection = SortOrder.None;
					HistorySortDirection = SortOrder.None;
				}

				switch (value)
				{
					case SortOrder.Increasing:
						Coins = _rootcoinlist.CreateDerivedCollection(x => x, x => true, (x, y) => x.Amount.CompareTo(y.Amount));
						break;

					case SortOrder.Decreasing:
						Coins = _rootcoinlist.CreateDerivedCollection(x => x, x => true, (x, y) => y.Amount.CompareTo(x.Amount));
						break;
				}
			}
		}

		public SortOrder PrivacySortDirection
		{
			get => _privacySortDirection;
			set
			{
				this.RaiseAndSetIfChanged(ref _privacySortDirection, value);
				if (value != SortOrder.None)
				{
					AmountSortDirection = SortOrder.None;
					StatusSortDirection = SortOrder.None;
					HistorySortDirection = SortOrder.None;
				}

				switch (value)
				{
					case SortOrder.Increasing:
						Coins = _rootcoinlist.CreateDerivedCollection(x => x, x => true, (x, y) => x.AnonymitySet.CompareTo(y.AnonymitySet));
						break;

					case SortOrder.Decreasing:
						Coins = _rootcoinlist.CreateDerivedCollection(x => x, x => true, (x, y) => y.AnonymitySet.CompareTo(x.AnonymitySet));
						break;
				}
			}
		}

		public SortOrder HistorySortDirection
		{
			get => _historySortDirection;
			set
			{
				this.RaiseAndSetIfChanged(ref _historySortDirection, value);
				if (value != SortOrder.None)
				{
					AmountSortDirection = SortOrder.None;
					StatusSortDirection = SortOrder.None;
					PrivacySortDirection = SortOrder.None;
				}

				switch (value)
				{
					case SortOrder.Increasing:
						Coins = _rootcoinlist.CreateDerivedCollection(x => x, x => true, (x, y) => string.Compare(x.History, y.History, StringComparison.Ordinal));
						break;

					case SortOrder.Decreasing:
						Coins = _rootcoinlist.CreateDerivedCollection(x => x, x => true, (x, y) => string.Compare(y.History, x.History, StringComparison.Ordinal));
						break;
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

		public CoinListViewModel(IReactiveDerivedList<CoinViewModel> coins, Money preSelectMinAmountIncludingCondition = null, int? preSelectMaxAnonSetExcludingCondition = null)
		{
			_rootcoinlist = coins;
			Coins = coins;

			if (preSelectMinAmountIncludingCondition != null && preSelectMaxAnonSetExcludingCondition != null)
			{
				foreach (CoinViewModel coin in coins)
				{
					if (coin.Amount >= preSelectMinAmountIncludingCondition && coin.AnonymitySet < preSelectMaxAnonSetExcludingCondition)
					{
						coin.IsSelected = true;
					}
				}
			}
			foreach (CoinViewModel coin in coins)
			{
				coin.PropertyChanged += Coin_PropertyChanged;
			}
			coins.CollectionChanging += Coins_CollectionChanging;
			coins.CollectionChanged += Coins_CollectionChanged;

			EnqueueCoin = ReactiveCommand.Create(() =>
			{
				if (SelectedCoin == null) return;
				//await Global.ChaumianClient.QueueCoinsToMixAsync()
			});

			DequeueCoin = ReactiveCommand.Create(() =>
			{
				if (SelectedCoin == null) return;
				DequeueCoinsPressed?.Invoke();
			}, this.WhenAnyValue(x => x.CanDeqeue));

			SelectAllCheckBoxCommand = ReactiveCommand.Create(() =>
			{
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
						SelectAllCoins(true, x => x.AnonymitySet >= 50);
						break;

					case false:
						SelectAllCoins(false, x => x.AnonymitySet >= 50);
						break;

					case null:
						SelectAllCoins(false, x => x.AnonymitySet >= 50);
						SelectPrivateCheckBoxState = false;
						break;
				}
			});

			SelectNonPrivateCheckBoxCommand = ReactiveCommand.Create(() =>
			{
				switch (SelectNonPrivateCheckBoxState)
				{
					case true:
						SelectAllCoins(true, x => x.AnonymitySet < 50);
						break;

					case false:
						SelectAllCoins(false, x => x.AnonymitySet < 50);
						break;

					case null:
						SelectAllCoins(false, x => x.AnonymitySet < 50);
						SelectNonPrivateCheckBoxState = false;
						break;
				}
			});
			SetSelections();
			SetCoinJoinStatusWidth();
			AmountSortDirection = SortOrder.Decreasing;
		}

		private void SetSelections()
		{
			SelectAllCheckBoxState = GetCheckBoxesSelectedState(x => true);
			SelectPrivateCheckBoxState = GetCheckBoxesSelectedState(x => x.AnonymitySet >= 50);
			SelectNonPrivateCheckBoxState = GetCheckBoxesSelectedState(x => x.AnonymitySet < 50);
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

		private void Coins_CollectionChanging(object sender, NotifyCollectionChangedEventArgs e)
		{
			foreach (CoinViewModel coin in Coins)
			{
				coin.PropertyChanged -= Coin_PropertyChanged;
			}
		}

		private void Coins_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			foreach (CoinViewModel coin in Coins)
			{
				coin.PropertyChanged += Coin_PropertyChanged;
			}
			SetSelections();
			SetCoinJoinStatusWidth();
		}

		private void Coin_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(CoinViewModel.IsSelected))
			{
				SetSelections();
			}
			if (e.PropertyName == nameof(CoinViewModel.Status))
			{
				SetCoinJoinStatusWidth();
			}
		}

		public IReactiveDerivedList<CoinViewModel> Coins
		{
			get { return _coins; }
			set { this.RaiseAndSetIfChanged(ref _coins, value); }
		}

#pragma warning restore CS0618 // Type or member is obsolete
	}
}
