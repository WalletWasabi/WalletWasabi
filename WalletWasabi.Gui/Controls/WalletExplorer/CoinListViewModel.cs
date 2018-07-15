using System;
using System.Collections.Generic;
using ReactiveUI;
using System.Collections.ObjectModel;
using WalletWasabi.Models;
using WalletWasabi.Gui.ViewModels;
using Avalonia;
using Avalonia.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class CoinListViewModel : ViewModelBase
	{
		private IReactiveDerivedList<CoinViewModel> _coins;

		public CoinListViewModel(IReactiveDerivedList<CoinViewModel> coins)
		{
			Coins = coins;
		}

		public IReactiveDerivedList<CoinViewModel> Coins
		{
			get { return _coins; }
			set { this.RaiseAndSetIfChanged(ref _coins, value); }
		}
	}
}
