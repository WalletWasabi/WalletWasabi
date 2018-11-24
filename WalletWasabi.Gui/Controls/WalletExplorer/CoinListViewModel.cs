using System.Collections.Generic;
using NBitcoin;
using ReactiveUI;
using ReactiveUI.Legacy;
using WalletWasabi.Gui.ViewModels;
using System.Linq;

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

		public ReactiveCommand EnqueueCoin { get; }
		public ReactiveCommand DequeueCoin { get; }
		public ReactiveCommand SelectAllCheckBoxCommand { get; }
		public CoinViewModel SelectedCoin
		{
			get => _selectedCoin;
			set
			{
				this.RaiseAndSetIfChanged(ref _selectedCoin, value);
				this.RaisePropertyChanged(nameof(CanDeqeue));
			}
		}

		public bool CanDeqeue
		{
			get
			{
				if (SelectedCoin == null) return false;
				return SelectedCoin.CoinJoinInProgress;
			}
		}

		public bool? SelectAllCheckBoxState
		{
			get
			{
				return _selectAllCheckBoxState;
			}
			set
			{
				var changed = _selectAllCheckBoxState != value;
				this.RaiseAndSetIfChanged(ref _selectAllCheckBoxState, value);
			}
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

		private bool? GetCheckBoxesSelectedState()
		{
			bool IsAllSelected = true;
			foreach (CoinViewModel coin in Coins)
				if (!coin.IsSelected)
				{
					IsAllSelected = false;
					break;
				}
			bool IsAllDeselected = true;
			foreach (CoinViewModel coin in Coins)
				if (coin.IsSelected)
				{
					IsAllDeselected = false;
					break;
				}
			if (IsAllSelected) return true;
			if (IsAllDeselected) return false;
			return null;
		}
		private void SelectAllCoins(bool valueOfSelected)
		{
			foreach (var c in Coins) c.IsSelected = valueOfSelected;
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
				coin.PropertyChanged += Coin_PropertyChanged;

			EnqueueCoin = ReactiveCommand.Create(() =>
			{
				if (SelectedCoin == null) return;
				//await Global.ChaumianClient.QueueCoinsToMixAsync()
			});

			DequeueCoin = ReactiveCommand.Create(async () =>
			{
				if (SelectedCoin == null) return;
				await Global.ChaumianClient.DequeueCoinsFromMixAsync(SelectedCoin.Model);
			}, this.WhenAnyValue(x => x.CanDeqeue));

			SelectAllCheckBoxCommand = ReactiveCommand.Create(() =>
			{
				switch (SelectAllCheckBoxState)
				{
					case true:
						SelectAllCoins(true);
						break;
					case false:
						SelectAllCoins(false);
						break;
					case null:
						SelectAllCoins(false);
						SelectAllCheckBoxState = false;
						break;
				}
			});
			SelectAllCheckBoxState = GetCheckBoxesSelectedState();
			AmountSortDirection = SortOrder.Decreasing;
		}

		void Coin_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(CoinViewModel.IsSelected))
			{
				SelectAllCheckBoxState = GetCheckBoxesSelectedState();
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
