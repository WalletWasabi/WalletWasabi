using NBitcoin;
using ReactiveUI;
using ReactiveUI.Legacy;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class CoinListViewModel : ViewModelBase
	{
#pragma warning disable CS0618 // Type or member is obsolete
		private IReactiveDerivedList<CoinViewModel> _coins;
		private CoinViewModel _selectedCoin;
		private bool? _selectAllCheckBoxState;

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
				this.RaiseAndSetIfChanged(ref _selectAllCheckBoxState,value);
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
