using System.Collections.Generic;
using ReactiveUI;
using System.Collections.ObjectModel;
using WalletWasabi.Models;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class ReadOnlyCoinListViewModel : CoinListViewModelBase
	{
		private IReactiveDerivedList<CoinViewModel> _coins;

		public ReadOnlyCoinListViewModel(IEnumerable<SmartCoin> coins)
		{
			SelectedCoins = new ObservableCollection<CoinViewModel>();

			Coins = coins.CreateDerivedCollection(c => new CoinViewModel(this, c), c => !c.SpentOrCoinJoinInProcess, (first, second) => first.Amount.CompareTo(second.Amount), RxApp.MainThreadScheduler);
		}

		public IReactiveDerivedList<CoinViewModel> Coins
		{
			get { return _coins; }
			set { this.RaiseAndSetIfChanged(ref _coins, value); }
		}
	}
}
