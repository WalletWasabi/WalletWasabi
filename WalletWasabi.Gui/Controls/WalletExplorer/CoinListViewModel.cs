using System;
using System.Collections.Generic;
using System.Text;
using ReactiveUI;
using System.Collections.ObjectModel;
using WalletWasabi.Models;
using System.Linq;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class CoinListViewModel : CoinListViewModelBase
	{
		private ObservableCollection<CoinViewModel> _coins;

		public CoinListViewModel(IEnumerable<SmartCoin> coins)
		{
			Coins = new ObservableCollection<CoinViewModel>(coins.Select(c => new CoinViewModel(this, c)));
		}

		public CoinListViewModel()
		{
			Coins = new ObservableCollection<CoinViewModel>();
		}

		public ObservableCollection<CoinViewModel> Coins
		{
			get { return _coins; }
			set { this.RaiseAndSetIfChanged(ref _coins, value); }
		}
	}
}
