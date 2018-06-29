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
		private IReadOnlyCollection<CoinViewModel> _availableCoins;
		private IReadOnlyCollection<CoinViewModel> _queuedCoins;
		private string _password;

		public CoinJoinTabViewModel(WalletViewModel walletViewModel)
			: base("CoinJoin", walletViewModel)
		{
			Password = "";

			AvailableCoins = Global.WalletService.Coins.CreateDerivedCollection(c => new CoinViewModel(c), c => !c.SpentOrCoinJoinInProcess && c.Confirmed, null, RxApp.MainThreadScheduler);

			QueuedCoins = Global.WalletService.Coins.CreateDerivedCollection(c => new CoinViewModel(c), c => c.CoinJoinInProcess, null, RxApp.MainThreadScheduler);

			AvailableCoinsList = new CoinListViewModel(AvailableCoins, (first, second) => second.Amount.CompareTo(first.Amount));

			QueuedCoinsList = new CoinListViewModel(QueuedCoins, (first, second) => second.Amount.CompareTo(first.Amount));

			EnqueueCommand = ReactiveCommand.Create(async () =>
			{
				var selectedCoins = AvailableCoinsList.Coins.Where(c => c.IsSelected).ToList();

				foreach (var coin in selectedCoins)
				{
					coin.IsSelected = false;
				}

				await Global.ChaumianClient.QueueCoinsToMixAsync(Password, selectedCoins.Select(c => c.Model).ToArray());
			});

			DequeueCommand = ReactiveCommand.Create(async () =>
			{
				var selectedCoins = QueuedCoinsList.Coins.Where(c => c.IsSelected).ToList();

				foreach (var coin in selectedCoins)
				{
					coin.IsSelected = false;
				}

				await Global.ChaumianClient.DequeueCoinsFromMixAsync(selectedCoins.Select(c => c.Model).ToArray());
			});
		}

		public override void OnSelected()
		{
			Global.ChaumianClient.ActivateFrequentStatusProcessing();
		}

		public override void OnDeselected()
		{
			Global.ChaumianClient.DeactivateFrequentStatusProcessingIfNotMixing();
		}

		public string Password
		{
			get { return _password; }
			set { this.RaiseAndSetIfChanged(ref _password, value); }
		}

		public IReadOnlyCollection<CoinViewModel> AvailableCoins
		{
			get { return _availableCoins; }
			set { this.RaiseAndSetIfChanged(ref _availableCoins, value); }
		}

		public IReadOnlyCollection<CoinViewModel> QueuedCoins
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
