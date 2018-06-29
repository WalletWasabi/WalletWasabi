using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Avalonia.Controls;
using ReactiveUI;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class CoinJoinTabViewModel : WalletActionViewModel
	{
		private CoinListViewModel _availableCoinsList;
		private CoinListViewModel _queuedCoinsList;
		private ObservableCollection<CoinViewModel> _availableCoins;
		private ObservableCollection<CoinViewModel> _queuedCoins;

		public CoinJoinTabViewModel(WalletViewModel walletViewModel)
			: base("CoinJoin", walletViewModel)
		{
			AvailableCoins = new ObservableCollection<CoinViewModel>(Global.WalletService.Coins.Where(c => !c.SpentOrCoinJoinInProcess)
				.Select(c => new CoinViewModel(c)));

			QueuedCoins = new ObservableCollection<CoinViewModel>();

			AvailableCoinsList = new CoinListViewModel(AvailableCoins);

			QueuedCoinsList = new CoinListViewModel(QueuedCoins);

			EnqueueCommand = ReactiveCommand.Create(() =>
			{
				var toMove = AvailableCoinsList.Coins.Where(c => c.IsSelected).ToList();

				foreach (var coin in toMove)
				{
					AvailableCoins.Remove(coin);
					QueuedCoins.Add(coin);
				}
			});

			DequeueCommand = ReactiveCommand.Create(() =>
			{
				var toMove = QueuedCoinsList.Coins.Where(c => c.IsSelected).ToList();

				foreach (var coin in toMove)
				{
					QueuedCoins.Remove(coin);
					AvailableCoins.Add(coin);
				}
			});
		}

		public ObservableCollection<CoinViewModel> AvailableCoins
		{
			get { return _availableCoins; }
			set { this.RaiseAndSetIfChanged(ref _availableCoins, value); }
		}

		public ObservableCollection<CoinViewModel> QueuedCoins
		{
			get { return _queuedCoins; }
			set { this.RaiseAndSetIfChanged(ref _queuedCoins, value); }
		}

		public CoinListViewModel AvailableCoinsList
		{
			get { return _availableCoinsList; }
			set { this.RaiseAndSetIfChanged(ref _availableCoinsList, value); }
		}

		public CoinListViewModel QueuedCoinsList
		{
			get { return _queuedCoinsList; }
			set { this.RaiseAndSetIfChanged(ref _queuedCoinsList, value); }
		}

		public ReactiveCommand EnqueueCommand { get; }

		public ReactiveCommand DequeueCommand { get; }
	}
}
