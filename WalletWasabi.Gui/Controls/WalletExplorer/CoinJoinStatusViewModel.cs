using AvalonStudio.Extensibility;
using AvalonStudio.MVVM;
using AvalonStudio.Shell;
using NBitcoin;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Composition;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Models.ChaumianCoinJoin;
using WalletWasabi.Services;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class CoinJoinStatusViewModel : ToolViewModel
	{
		private long _roundId;
		private CcjRoundPhase _phase;
		private DateTimeOffset _roundTimesout;
		private TimeSpan _timeLeftTillRoundTimeout;
		private Money _requiredBTC;
		private string _coordinatorFeePercent;
		private int _peersRegistered;
		private int _peersNeeded;

		public override Location DefaultLocation => Location.Right;

		public CoinJoinStatusViewModel()
		{
			Title = "CoinJoin Status";

			TimeLeftTillRoundTimeout = TimeSpan.Zero;

			this.WhenAnyValue(x => x.RoundTimesout)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x =>
				{
					TimeSpan left = x - DateTimeOffset.UtcNow;
					TimeLeftTillRoundTimeout = left > TimeSpan.Zero ? left : TimeSpan.Zero; // Make sure cannot be less than zero.
				});

			this.WhenAnyValue(x => x.TimeLeftTillRoundTimeout)
				.Throttle(TimeSpan.FromSeconds(1))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x =>
				{
					var next = TimeLeftTillRoundTimeout - TimeSpan.FromSeconds(1);
					TimeLeftTillRoundTimeout = next > TimeSpan.Zero ? next : TimeSpan.Zero; // Make sure cannot be less than zero.
				});

			RxApp.MainThreadScheduler.Schedule(async () =>
			{
				while(Global.ChaumianClient == null)
				{
					await Task.Delay(100);
				}

				Initialise();
			});
		}

		private void Initialise()
		{
			var registrableRound = Global.ChaumianClient.State.GetRegistrableRoundOrDefault();

			UpdateRequiredBtcLabel(registrableRound);

			CoordinatorFeePercent = registrableRound?.State?.CoordinatorFeePercent.ToString() ?? "0.003";

			CcjClientRound mostAdvancedRound = Global.ChaumianClient?.State?.GetMostAdvancedRoundOrDefault();

			if (mostAdvancedRound != default)
			{
				RoundId = mostAdvancedRound.State.RoundId;
				Phase = mostAdvancedRound.State.Phase;
				RoundTimesout = mostAdvancedRound.State.Phase == CcjRoundPhase.InputRegistration ? mostAdvancedRound.State.InputRegistrationTimesout : DateTimeOffset.UtcNow;
				PeersRegistered = mostAdvancedRound.State.RegisteredPeerCount;
				PeersNeeded = mostAdvancedRound.State.RequiredPeerCount;
			}
			else
			{
				RoundId = -1;
				Phase = CcjRoundPhase.InputRegistration;
				RoundTimesout = DateTimeOffset.UtcNow;
				PeersRegistered = 0;
				PeersNeeded = 100;
			}
		}

		public long RoundId
		{
			get => _roundId;
			set => this.RaiseAndSetIfChanged(ref _roundId, value);
		}

		public CcjRoundPhase Phase
		{
			get => _phase;
			set => this.RaiseAndSetIfChanged(ref _phase, value);
		}

		public DateTimeOffset RoundTimesout
		{
			get => _roundTimesout;
			set => this.RaiseAndSetIfChanged(ref _roundTimesout, value);
		}

		public TimeSpan TimeLeftTillRoundTimeout
		{
			get => _timeLeftTillRoundTimeout;
			set => this.RaiseAndSetIfChanged(ref _timeLeftTillRoundTimeout, value);
		}

		public Money RequiredBTC
		{
			get => _requiredBTC;
			set => this.RaiseAndSetIfChanged(ref _requiredBTC, value);
		}

		public string CoordinatorFeePercent
		{
			get => _coordinatorFeePercent;
			set => this.RaiseAndSetIfChanged(ref _coordinatorFeePercent, value);
		}

		public int PeersRegistered
		{
			get => _peersRegistered;
			set => this.RaiseAndSetIfChanged(ref _peersRegistered, value);
		}

		public int PeersNeeded
		{
			get => _peersNeeded;
			set => this.RaiseAndSetIfChanged(ref _peersNeeded, value);
		}

		private void UpdateStates()
		{
			var registrableRound = Global.ChaumianClient.State.GetRegistrableRoundOrDefault();
			if (registrableRound != default)
			{
				CoordinatorFeePercent = registrableRound.State.CoordinatorFeePercent.ToString();
				UpdateRequiredBtcLabel(registrableRound);
			}
			var mostAdvancedRound = Global.ChaumianClient.State.GetMostAdvancedRoundOrDefault();
			if (mostAdvancedRound != default)
			{
				RoundId = mostAdvancedRound.State.RoundId;
				if (!Global.ChaumianClient.State.IsInErrorState)
				{
					Phase = mostAdvancedRound.State.Phase;
					RoundTimesout = mostAdvancedRound.State.Phase == CcjRoundPhase.InputRegistration ? mostAdvancedRound.State.InputRegistrationTimesout : DateTimeOffset.UtcNow;
				}
				this.RaisePropertyChanged(nameof(Phase));
				this.RaisePropertyChanged(nameof(RoundTimesout));
				PeersRegistered = mostAdvancedRound.State.RegisteredPeerCount;
				PeersNeeded = mostAdvancedRound.State.RequiredPeerCount;
			}
		}

#pragma warning disable CS0618 // Type or member is obsolete

		private void UpdateRequiredBtcLabel(CcjClientRound registrableRound)
#pragma warning restore CS0618 // Type or member is obsolete
		{
			if (Global.WalletService is null)
			{
				return; // Otherwise NullReferenceException at shutdown.
			}

			if (registrableRound == default)
			{
				if (RequiredBTC == default)
				{
					RequiredBTC = Money.Zero;
				}
			}
			else
			{
				var queued = Global.WalletService.Coins.Where(x => x.CoinJoinInProgress);
				if (queued.Any())
				{
					RequiredBTC = registrableRound.State.CalculateRequiredAmount(Global.ChaumianClient.State.GetAllQueuedCoinAmounts().ToArray());
				}
				else
				{
					var available = Global.WalletService.Coins.Where(x => x.Confirmed && !x.Unavailable);
					if (available.Any())
					{
						RequiredBTC = registrableRound.State.CalculateRequiredAmount(available.Where(x => x.AnonymitySet < Global.Config.PrivacyLevelStrong).Select(x => x.Amount).ToArray());
					}
					else
					{
						RequiredBTC = registrableRound.State.CalculateRequiredAmount();
					}
				}
			}
		}

		public Global Global => AvaloniaGlobalComponent.AvaloniaInstance;
	}
}
