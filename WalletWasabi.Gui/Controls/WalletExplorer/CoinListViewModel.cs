using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Gui.ViewModels;
using ReactiveUI;
using System.Collections.ObjectModel;
using WalletWasabi.Models;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class CoinListViewModel : ViewModelBase
	{
		private ObservableCollection<CoinViewModel> _selectedCoins;
		private IEnumerable<CoinViewModel> _coins;

		public CoinListViewModel()
		{
			SelectedCoins = new ObservableCollection<CoinViewModel>();

			Coins = Global.WalletService.Coins.CreateDerivedCollection(c => new CoinViewModel(this, c), c => !c.SpentOrCoinJoinInProcess, (first, second) => first.Amount.CompareTo(second.Amount), RxApp.MainThreadScheduler);
		}

		public ObservableCollection<CoinViewModel> SelectedCoins
		{
			get { return _selectedCoins; }
			set { this.RaiseAndSetIfChanged(ref _selectedCoins, value); }
		}

		public IEnumerable<CoinViewModel> Coins
		{
			get { return _coins; }
			set { this.RaiseAndSetIfChanged(ref _coins, value); }
		}
	}
}
