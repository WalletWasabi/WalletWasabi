using System;
using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.CoinJoin.Client.Rounds;
using WalletWasabi.CoinJoin.Common.Models;
using WalletWasabi.Gui.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles
{
	public partial class RoundStatusTileViewModel : TileViewModel
	{
		private readonly Wallet _wallet;
		[AutoNotify] private long _roundId;
		[AutoNotify] private RoundPhaseState _roundPhaseState;
		[AutoNotify] private DateTimeOffset _roundTimesout;
		[AutoNotify] private string _timeLeftTillRoundTimeout;
		private Money _requiredBTC;
		private string _coordinatorFeePercent;
		[AutoNotify] private int _peersRegistered;
		[AutoNotify] private int _peersNeeded;

		[AutoNotify] private int _selectedIndex;

		public RoundStatusTileViewModel(Wallet wallet)
		{
			_wallet = wallet;

			Observable.FromEventPattern(_wallet.ChaumianClient, nameof(Wallet.ChaumianClient.CoinQueued))
				.Merge(Observable.FromEventPattern(_wallet.ChaumianClient, nameof(Wallet.ChaumianClient.OnDequeue)))
				.Merge(Observable.FromEventPattern(_wallet.ChaumianClient, nameof(Wallet.ChaumianClient.StateUpdated)))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => UpdateStates());
				//.DisposeWith(disposables);

			ClientRound mostAdvancedRound = _wallet.ChaumianClient?.State?.GetMostAdvancedRoundOrDefault();

			if (mostAdvancedRound is { })
			{
				RoundId = mostAdvancedRound.State.RoundId;
				RoundPhaseState = new RoundPhaseState(mostAdvancedRound.State.Phase,
					_wallet.ChaumianClient?.State.IsInErrorState ?? false);
				RoundTimesout = mostAdvancedRound.State.Phase == RoundPhase.InputRegistration
					? mostAdvancedRound.State.InputRegistrationTimesout
					: DateTimeOffset.UtcNow;
				PeersRegistered = mostAdvancedRound.State.RegisteredPeerCount;
				PeersNeeded = mostAdvancedRound.State.RequiredPeerCount;
			}
			else
			{
				RoundId = -1;
				RoundPhaseState = new RoundPhaseState(RoundPhase.InputRegistration, false);
				RoundTimesout = DateTimeOffset.UtcNow;
				PeersRegistered = 0;
				PeersNeeded = 100;
			}

			Observable.Interval(TimeSpan.FromSeconds(1))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ =>
				{
					TimeSpan left = RoundTimesout - DateTimeOffset.UtcNow;
					TimeLeftTillRoundTimeout =
						(left > TimeSpan.Zero ? left : TimeSpan.Zero).ToString("hh\\:mm\\:ss"); // Make sure cannot be less than zero.
				}); //.DisposeWith(disposables);

			Observable.Interval(TimeSpan.FromSeconds(8))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ =>
				{
					var index = SelectedIndex;

					index++;

					if (index > 3)
					{
						index = 0;
					}

					SelectedIndex = index;
				});
		}

		private void UpdateStates()
		{
			var chaumianClient = _wallet?.ChaumianClient;
			if (chaumianClient is null)
			{
				return;
			}

			//AmountQueued = chaumianClient.State.SumAllQueuedCoinAmounts();

			var registrableRound = chaumianClient.State.GetRegistrableRoundOrDefault();
			if (registrableRound is { })
			{
				//CoordinatorFeePercent = registrableRound.State.CoordinatorFeePercent.ToString();
				//UpdateRequiredBtcLabel(registrableRound);
			}

			var mostAdvancedRound = chaumianClient.State.GetMostAdvancedRoundOrDefault();
			if (mostAdvancedRound is { })
			{
				RoundId = mostAdvancedRound.State.RoundId;
				if (!chaumianClient.State.IsInErrorState)
				{
					RoundPhaseState = new RoundPhaseState(mostAdvancedRound.State.Phase, false);
					RoundTimesout = mostAdvancedRound.State.Phase == RoundPhase.InputRegistration
						? mostAdvancedRound.State.InputRegistrationTimesout
						: DateTimeOffset.UtcNow;
				}
				else
				{
					RoundPhaseState = new RoundPhaseState(RoundPhaseState.Phase, true);
				}

				this.RaisePropertyChanged(nameof(RoundPhaseState));
				this.RaisePropertyChanged(nameof(RoundTimesout));
				PeersRegistered = mostAdvancedRound.State.RegisteredPeerCount;
				PeersNeeded = mostAdvancedRound.State.RequiredPeerCount;
			}
		}
	}
}