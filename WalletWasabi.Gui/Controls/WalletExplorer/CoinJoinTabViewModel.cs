using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Avalonia.Controls;
using ReactiveUI;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class CoinJoinTabViewModel : WalletActionViewModel
	{
		private CoinListViewModel _availableCoins;
		private CoinListViewModel _queuedCoins;

		public CoinJoinTabViewModel(WalletViewModel walletViewModel)
			: base("CoinJoin", walletViewModel)
		{
			AvailableCoins = new CoinListViewModel(Global.WalletService.Coins);

			QueuedCoins = new CoinListViewModel();

			EnqueueCommand = ReactiveCommand.Create(() =>
			{
				var toMove = AvailableCoins.SelectedCoins.ToList();

				foreach (var coin in toMove)
				{
					coin.ChangeOwner(QueuedCoins);
					AvailableCoins.Coins.Remove(coin);
					QueuedCoins.Coins.Add(coin);
				}
			});

			DequeueCommand = ReactiveCommand.Create(() =>
			{
				var toMove = QueuedCoins.SelectedCoins.ToList();

				foreach (var coin in toMove)
				{
					coin.ChangeOwner(AvailableCoins);
					QueuedCoins.Coins.Remove(coin);
					AvailableCoins.Coins.Add(coin);
				}
			});
		}

		public CoinListViewModel AvailableCoins
		{
			get { return _availableCoins; }
			set { this.RaiseAndSetIfChanged(ref _availableCoins, value); }
		}

		public CoinListViewModel QueuedCoins
		{
			get { return _queuedCoins; }
			set { this.RaiseAndSetIfChanged(ref _queuedCoins, value); }
		}

		public ReactiveCommand EnqueueCommand { get; }

		public ReactiveCommand DequeueCommand { get; }
	}
}
