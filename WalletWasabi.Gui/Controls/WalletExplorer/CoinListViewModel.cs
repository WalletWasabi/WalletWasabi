using System.Collections.Generic;
using ReactiveUI;
using System.Collections.ObjectModel;
using WalletWasabi.Models;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class CoinListViewModel : ViewModelBase
	{
		private IReactiveDerivedList<CoinViewModel> _coins;

		public CoinListViewModel(IEnumerable<CoinViewModel> coins)
		{
			Coins = coins.CreateDerivedCollection(c => c, null, (first, second) => first.Amount.CompareTo(second.Amount), RxApp.MainThreadScheduler);
		}

		public IReactiveDerivedList<CoinViewModel> Coins
		{
			get { return _coins; }
			set { this.RaiseAndSetIfChanged(ref _coins, value); }
		}
	}
}
