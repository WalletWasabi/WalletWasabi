using NBitcoin;
using ReactiveUI;
using ReactiveUI.Legacy;
using System;
using System.Linq;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class CoinListViewModel : ViewModelBase
	{
#pragma warning disable CS0618 // Type or member is obsolete
		private IReactiveDerivedList<CoinViewModel> _coins;
		private CoinViewModel _selectedCoin;
		private bool? _selectAllCheckBoxState;
		private bool? _selectPrivateCheckBoxState;
		private bool? _selectNonPrivateCheckBoxState;

		public ReactiveCommand EnqueueCoin { get; }
		public ReactiveCommand DequeueCoin { get; }
		public ReactiveCommand SelectAllCheckBoxCommand { get; }
		public ReactiveCommand SelectPrivateCheckBoxCommand { get; }
		public ReactiveCommand SelectNonPrivateCheckBoxCommand { get; }

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
				this.RaiseAndSetIfChanged(ref _selectAllCheckBoxState, value);
			}
		}

		public bool? SelectPrivateCheckBoxState
		{
			get
			{
				return _selectPrivateCheckBoxState;
			}
			set
			{
				this.RaiseAndSetIfChanged(ref _selectPrivateCheckBoxState, value);
			}
		}

		public bool? SelectNonPrivateCheckBoxState
		{
			get
			{
				return _selectNonPrivateCheckBoxState;
			}
			set
			{
				this.RaiseAndSetIfChanged(ref _selectNonPrivateCheckBoxState, value);
			}
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
			Coins = coins;

			if (preSelectMinAmountIncludingCondition != null && preSelectMaxAnonSetExcludingCondition != null)
			{
				foreach (CoinViewModel coin in Coins)
				{
					if (coin.Amount >= preSelectMinAmountIncludingCondition && coin.AnonymitySet < preSelectMaxAnonSetExcludingCondition)
					{
						coin.IsSelected = true;
					}
				}
			}

			foreach (CoinViewModel coin in Coins)
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

			SelectAllCheckBoxState = GetCheckBoxesSelectedState(x => true);
			SelectPrivateCheckBoxState = GetCheckBoxesSelectedState(x => x.AnonymitySet >= 50);
			SelectNonPrivateCheckBoxState = GetCheckBoxesSelectedState(x => x.AnonymitySet < 50);
		}

		private void Coin_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(CoinViewModel.IsSelected))
			{
				SelectAllCheckBoxState = GetCheckBoxesSelectedState(x => true);
				SelectPrivateCheckBoxState = GetCheckBoxesSelectedState(x => x.AnonymitySet >= 50);
				SelectNonPrivateCheckBoxState = GetCheckBoxesSelectedState(x => x.AnonymitySet < 50);
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
