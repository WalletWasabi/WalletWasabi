using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using Avalonia.Controls;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Models.ChaumianCoinJoin;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class CoinJoinTabViewModel : WalletActionViewModel
	{
		private CoinListViewModel _availableCoinsList;
		private CoinListViewModel _queuedCoinsList;
		private long _roundId;
		private string _phase;
		private Money _requiredBTC;
		private string _coordinatorFeePercent;
		private int _peersRegistered;
		private int _peersNeeded;
		private string _password;
		private Money _amountQueued;

		public CoinJoinTabViewModel(WalletViewModel walletViewModel)
			: base("CoinJoin", walletViewModel)
		{
			Password = "";

			var onCoinsSetModified = Observable.FromEventPattern(Global.WalletService.Coins, nameof(Global.WalletService.Coins.HashSetChanged))
				.ObserveOn(RxApp.MainThreadScheduler);

			var globalCoins = Global.WalletService.Coins.CreateDerivedCollection(c => new CoinViewModel(c), null, (first, second) => second.Amount.CompareTo(first.Amount), signalReset: onCoinsSetModified, RxApp.MainThreadScheduler);
			globalCoins.ChangeTrackingEnabled = true;

			var available = globalCoins.CreateDerivedCollection(c => c, c => c.Confirmed && !c.SpentOrCoinJoinInProcess);

			var queued = globalCoins.CreateDerivedCollection(c => c, c => c.CoinJoinInProgress);

			AvailableCoinsList = new CoinListViewModel(available);

			QueuedCoinsList = new CoinListViewModel(queued);

			AmountQueued = QueuedCoinsList.Coins.Sum(x => x.Amount);

			QueuedCoinsList.Coins.CollectionChanged += Coins_CollectionChanged;

			var registrableRound = Global.ChaumianClient.State.GetRegistrableRoundOrDefault();
			if (registrableRound != default)
			{
				CoordinatorFeePercent = registrableRound.State.CoordinatorFeePercent.ToString();
				RequiredBTC = registrableRound.State.Denomination + registrableRound.State.Denomination.Percentange(0.3m) + registrableRound.State.FeePerInputs * registrableRound.State.MaximumInputCountPerPeer + registrableRound.State.FeePerOutputs * 2;
			}
			else
			{
				CoordinatorFeePercent = "0.3";
				RequiredBTC = Money.Zero;
			}

			var mostAdvancedRound = Global.ChaumianClient.State.GetMostAdvancedRoundOrDefault();
			if (mostAdvancedRound != default)
			{
				RoundId = mostAdvancedRound.State.RoundId;
				Phase = mostAdvancedRound.State.Phase.ToString();
				PeersRegistered = mostAdvancedRound.State.RegisteredPeerCount;
				PeersNeeded = mostAdvancedRound.State.RequiredPeerCount;
			}
			else
			{
				RoundId = -1;
				Phase = CcjRoundPhase.InputRegistration.ToString();
				PeersRegistered = 0;
				PeersNeeded = 100;
			}
			Global.ChaumianClient.StateUpdated += ChaumianClient_StateUpdated;

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

		private void Coins_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			AmountQueued = QueuedCoinsList.Coins.Sum(x => x.Amount);
		}

		private void ChaumianClient_StateUpdated(object sender, EventArgs e)
		{
			var registrableRound = Global.ChaumianClient.State.GetRegistrableRoundOrDefault();
			if (registrableRound != default)
			{
				CoordinatorFeePercent = registrableRound.State.CoordinatorFeePercent.ToString();
				RequiredBTC = registrableRound.State.Denomination + registrableRound.State.Denomination.Percentange(0.3m) + registrableRound.State.FeePerInputs * registrableRound.State.MaximumInputCountPerPeer + registrableRound.State.FeePerOutputs * 2;
			}
			var mostAdvancedRound = Global.ChaumianClient.State.GetMostAdvancedRoundOrDefault();
			if (mostAdvancedRound != default)
			{
				RoundId = mostAdvancedRound.State.RoundId;
				Phase = mostAdvancedRound.State.Phase.ToString();
				PeersRegistered = mostAdvancedRound.State.RegisteredPeerCount;
				PeersNeeded = mostAdvancedRound.State.RequiredPeerCount;
			}
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

		public Money AmountQueued
		{
			get { return _amountQueued; }
			set { this.RaiseAndSetIfChanged(ref _amountQueued, value); }
		}

		public long RoundId
		{
			get { return _roundId; }
			set { this.RaiseAndSetIfChanged(ref _roundId, value); }
		}

		public string Phase
		{
			get { return _phase; }
			set { this.RaiseAndSetIfChanged(ref _phase, value); }
		}

		public Money RequiredBTC
		{
			get { return _requiredBTC; }
			set { this.RaiseAndSetIfChanged(ref _requiredBTC, value); }
		}

		public string CoordinatorFeePercent
		{
			get { return _coordinatorFeePercent; }
			set { this.RaiseAndSetIfChanged(ref _coordinatorFeePercent, value); }
		}

		public int PeersRegistered
		{
			get { return _peersRegistered; }
			set { this.RaiseAndSetIfChanged(ref _peersRegistered, value); }
		}

		public int PeersNeeded
		{
			get { return _peersNeeded; }
			set { this.RaiseAndSetIfChanged(ref _peersNeeded, value); }
		}

		public ReactiveCommand EnqueueCommand { get; }

		public ReactiveCommand DequeueCommand { get; }
	}
}
