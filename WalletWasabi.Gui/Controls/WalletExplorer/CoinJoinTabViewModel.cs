using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Models.ChaumianCoinJoin;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class CoinJoinTabViewModel : WalletActionViewModel
	{
		private CoinListViewModel _availableCoinsList;
		private CoinListViewModel _queuedCoinsList;
		private long _roundId;
		private CcjRoundPhase _phase;
		private Money _requiredBTC;
		private string _coordinatorFeePercent;
		private int _peersRegistered;
		private int _peersNeeded;
		private string _password;
		private Money _amountQueued;
		private string _warningMessageEnqueue;
		private string _warningMessageDequeue;

		public CoinJoinTabViewModel(WalletViewModel walletViewModel)
			: base("CoinJoin", walletViewModel)
		{
			Password = "";

			var onCoinsSetModified = Observable.FromEventPattern(Global.WalletService.Coins, nameof(Global.WalletService.Coins.HashSetChanged))
				.ObserveOn(RxApp.MainThreadScheduler);

			var globalCoins = Global.WalletService.Coins.CreateDerivedCollection(c => new CoinViewModel(c), null, (first, second) => second.Amount.CompareTo(first.Amount), signalReset: onCoinsSetModified, RxApp.MainThreadScheduler);
			globalCoins.ChangeTrackingEnabled = true;

			var available = globalCoins.CreateDerivedCollection(c => c, c => c.Confirmed && !c.SpentOrCoinJoinInProgress);

			var queued = globalCoins.CreateDerivedCollection(c => c, c => c.CoinJoinInProgress);

			AvailableCoinsList = new CoinListViewModel(available);

			QueuedCoinsList = new CoinListViewModel(queued);

			AmountQueued = Global.ChaumianClient.State.SumAllQueuedCoinAmounts();

			Global.ChaumianClient.CoinQueued += ChaumianClient_CoinQueued;
			Global.ChaumianClient.CoinDequeued += ChaumianClient_CoinDequeued;

			var registrableRound = Global.ChaumianClient.State.GetRegistrableRoundOrDefault();
			if (registrableRound != default)
			{
				CoordinatorFeePercent = registrableRound.State.CoordinatorFeePercent.ToString();
				RequiredBTC = registrableRound.State.CalculateRequiredAmount(Global.ChaumianClient.State.GetAllQueuedCoinAmounts().ToArray());
			}
			else
			{
				CoordinatorFeePercent = "0.003";
				RequiredBTC = Money.Zero;
			}

			var mostAdvancedRound = Global.ChaumianClient.State.GetMostAdvancedRoundOrDefault();
			if (mostAdvancedRound != default)
			{
				RoundId = mostAdvancedRound.State.RoundId;
				Phase = mostAdvancedRound.State.Phase;
				PeersRegistered = mostAdvancedRound.State.RegisteredPeerCount;
				PeersNeeded = mostAdvancedRound.State.RequiredPeerCount;
			}
			else
			{
				RoundId = -1;
				Phase = CcjRoundPhase.InputRegistration;
				PeersRegistered = 0;
				PeersNeeded = 100;
			}
			Global.ChaumianClient.StateUpdated += ChaumianClient_StateUpdated;

			EnqueueCommand = ReactiveCommand.Create(async () =>
			{
				await DoEnqueueAsync();
			});

			DequeueCommand = ReactiveCommand.Create(async () =>
			{
				var selectedCoins = QueuedCoinsList.Coins.Where(c => c.IsSelected).ToList();

				foreach (var coin in selectedCoins)
				{
					coin.IsSelected = false;
				}

				try
				{
					await Global.ChaumianClient.DequeueCoinsFromMixAsync(selectedCoins.Select(c => c.Model).ToArray());
				}
				catch (Exception ex)
				{
					Logger.LogWarning<CoinJoinTabViewModel>(ex);
					WarningMessageDequeue = ex.ToTypeMessageString();
					if (ex is AggregateException aggex)
					{
						foreach (var iex in aggex.InnerExceptions)
						{
							WarningMessageDequeue += Environment.NewLine + iex.ToTypeMessageString();
						}
					}
					return;
				}

				WarningMessageDequeue = string.Empty;
			});

			this.WhenAnyValue(x => x.Password).Subscribe(async x =>
			{
				if (x.NotNullAndNotEmpty())
				{
					char lastChar = x.Last();
					if (lastChar == '\r' || lastChar == '\n') // If the last character is cr or lf then act like it'd be a sign to do the job.
					{
						Password = x.TrimEnd('\r', '\n');
						await DoEnqueueAsync();
					}
				}
			});
		}

		private async Task DoEnqueueAsync()
		{
			Password = Guard.Correct(Password);
			var selectedCoins = AvailableCoinsList.Coins.Where(c => c.IsSelected).ToList();

			if (!selectedCoins.Any())
			{
				WarningMessageEnqueue = "No coins are selected to enqueue.";
				return;
			}

			WarningMessageEnqueue = string.Empty;

			try
			{
				await Global.ChaumianClient.QueueCoinsToMixAsync(Password, selectedCoins.Select(c => c.Model).ToArray());
			}
			catch (Exception ex)
			{
				Logger.LogWarning<CoinJoinTabViewModel>(ex);
				WarningMessageEnqueue = ex.ToTypeMessageString();
				if (ex is AggregateException aggex)
				{
					foreach (var iex in aggex.InnerExceptions)
					{
						WarningMessageEnqueue += Environment.NewLine + iex.ToTypeMessageString();
					}
				}
				Password = string.Empty;
				return;
			}

			Password = string.Empty;
			WarningMessageEnqueue = string.Empty;

			foreach (var coin in selectedCoins)
			{
				coin.IsSelected = false;
			}
		}

		private void ChaumianClient_CoinDequeued(object sender, SmartCoin e)
		{
			UpdateStates();
		}

		private void ChaumianClient_CoinQueued(object sender, SmartCoin e)
		{
			UpdateStates();
		}

		private void ChaumianClient_StateUpdated(object sender, EventArgs e)
		{
			UpdateStates();
		}

		private void UpdateStates()
		{
			AmountQueued = Global.ChaumianClient.State.SumAllQueuedCoinAmounts();
			MainWindowViewModel.Instance.CanClose = AmountQueued == Money.Zero;

			var registrableRound = Global.ChaumianClient.State.GetRegistrableRoundOrDefault();
			if (registrableRound != default)
			{
				CoordinatorFeePercent = registrableRound.State.CoordinatorFeePercent.ToString();
				RequiredBTC = registrableRound.State.CalculateRequiredAmount(Global.ChaumianClient.State.GetAllQueuedCoinAmounts().ToArray());
			}
			var mostAdvancedRound = Global.ChaumianClient.State.GetMostAdvancedRoundOrDefault();
			if (mostAdvancedRound != default)
			{
				RoundId = mostAdvancedRound.State.RoundId;
				Phase = mostAdvancedRound.State.Phase;
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

		public CcjRoundPhase Phase
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

		public string WarningMessageEnqueue
		{
			get { return _warningMessageEnqueue; }
			set { this.RaiseAndSetIfChanged(ref _warningMessageEnqueue, value); }
		}

		public string WarningMessageDequeue
		{
			get { return _warningMessageDequeue; }
			set { this.RaiseAndSetIfChanged(ref _warningMessageDequeue, value); }
		}

		public ReactiveCommand EnqueueCommand { get; }

		public ReactiveCommand DequeueCommand { get; }
	}
}
