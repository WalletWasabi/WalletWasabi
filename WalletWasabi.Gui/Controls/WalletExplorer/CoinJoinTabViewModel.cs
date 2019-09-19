using Avalonia.Threading;
using NBitcoin;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Gui.Models;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Gui.ViewModels.Validation;
using WalletWasabi.Helpers;
using WalletWasabi.KeyManagement;
using WalletWasabi.Logging;
using WalletWasabi.Models.ChaumianCoinJoin;
using WalletWasabi.Services;
using WalletWasabi.Models;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class CoinJoinTabViewModel : WalletActionViewModel
	{
		private CompositeDisposable Disposables { get; set; }

		private long _roundId;
		private CcjRoundPhase _phase;
		private DateTimeOffset _roundTimesout;
		private TimeSpan _timeLeftTillRoundTimeout;
		private Money _requiredBTC;
		private string _coordinatorFeePercent;
		private int _peersRegistered;
		private int _peersNeeded;
		private string _password;
		private Money _amountQueued;
		private bool _isEnqueueBusy;
		private bool _isDequeueBusy;
		private string _enqueueButtonText;
		private const string EnqueueButtonTextString = "Enqueue Selected Coins";
		private const string EnqueuingButtonTextString = "Queuing coins...";
		private string _dequeueButtonText;
		private const string DequeueButtonTextString = "Dequeue Selected Coins";
		private const string DequeuingButtonTextString = "Dequeuing coins...";
		private int _coinJoinUntilAnonymitySet;
		private TargetPrivacy _targetPrivacy;

		public CoinJoinTabViewModel(WalletViewModel walletViewModel)
			: base("CoinJoin", walletViewModel)
		{
			Password = "";
			TimeLeftTillRoundTimeout = TimeSpan.Zero;

			CoinsList = new CoinListViewModel(Global, CoinListContainerType.CoinJoinTabViewModel);

			Observable.FromEventPattern(CoinsList, nameof(CoinsList.DequeueCoinsPressed)).Subscribe(_ => OnCoinsListDequeueCoinsPressedAsync());

			AmountQueued = Money.Zero; // Global.ChaumianClient.State.SumAllQueuedCoinAmounts();

			EnqueueCommand = ReactiveCommand.CreateFromTask(async () => await DoEnqueueAsync(CoinsList.Coins.Where(c => c.IsSelected)));

			DequeueCommand = ReactiveCommand.CreateFromTask(async () => await DoDequeueAsync(CoinsList.Coins.Where(c => c.IsSelected)));

			PrivacySomeCommand = ReactiveCommand.Create(() => TargetPrivacy = TargetPrivacy.Some);

			PrivacyFineCommand = ReactiveCommand.Create(() => TargetPrivacy = TargetPrivacy.Fine);

			PrivacyStrongCommand = ReactiveCommand.Create(() => TargetPrivacy = TargetPrivacy.Strong);

			TargetButtonCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				switch (TargetPrivacy)
				{
					case TargetPrivacy.None:
						TargetPrivacy = TargetPrivacy.Some;
						break;

					case TargetPrivacy.Some:
						TargetPrivacy = TargetPrivacy.Fine;
						break;

					case TargetPrivacy.Fine:
						TargetPrivacy = TargetPrivacy.Strong;
						break;

					case TargetPrivacy.Strong:
						TargetPrivacy = TargetPrivacy.Some;
						break;
				}
				Global.Config.MixUntilAnonymitySet = CoinJoinUntilAnonymitySet;
				await Global.Config.ToFileAsync();
			});

			this.WhenAnyValue(x => x.IsEnqueueBusy)
				.Select(x => x ? EnqueuingButtonTextString : EnqueueButtonTextString)
				.Subscribe(text => EnqueueButtonText = text);

			this.WhenAnyValue(x => x.IsDequeueBusy)
				.Select(x => x ? DequeuingButtonTextString : DequeueButtonTextString)
				.Subscribe(text => DequeueButtonText = text);

			this.WhenAnyValue(x => x.TargetPrivacy)
				.Subscribe(target => CoinJoinUntilAnonymitySet = Global.Config.GetTargetLevel(target));

			this.WhenAnyValue(x => x.RoundTimesout)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x =>
				{
					TimeSpan left = x - DateTimeOffset.UtcNow;
					TimeLeftTillRoundTimeout = left > TimeSpan.Zero ? left : TimeSpan.Zero; // Make sure cannot be less than zero.
				});
		}

		public override void OnOpen()
		{
			base.OnOpen();

			Disposables = Disposables is null ? new CompositeDisposable() : throw new NotSupportedException($"Cannot open {GetType().Name} before closing it.");

			TargetPrivacy = Global.Config.GetTargetPrivacy();

			var registrableRound = Global.ChaumianClient.State.GetRegistrableRoundOrDefault();

			UpdateRequiredBtcLabel(registrableRound);

			CoordinatorFeePercent = registrableRound?.State?.CoordinatorFeePercent.ToString() ?? "0.003";

			Observable.FromEventPattern(Global.ChaumianClient, nameof(Global.ChaumianClient.CoinQueued))
				.Merge(Observable.FromEventPattern(Global.ChaumianClient, nameof(Global.ChaumianClient.CoinDequeued)))
				.Merge(Observable.FromEventPattern(Global.ChaumianClient, nameof(Global.ChaumianClient.StateUpdated)))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => UpdateStates())
				.DisposeWith(Disposables);

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

			Global.UiConfig.WhenAnyValue(x => x.LurkingWifeMode).ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ =>
				{
					this.RaisePropertyChanged(nameof(AmountQueued));
					this.RaisePropertyChanged(nameof(IsLurkingWifeMode));
				})
				.DisposeWith(Disposables);

			Observable.Interval(TimeSpan.FromSeconds(1))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ =>
				{
					TimeSpan left = RoundTimesout - DateTimeOffset.UtcNow;
					TimeLeftTillRoundTimeout = left > TimeSpan.Zero ? left : TimeSpan.Zero; // Make sure cannot be less than zero.
				})
				.DisposeWith(Disposables);
		}

		public override bool OnClose()
		{
			CoinsList.OnClose();

			Disposables?.Dispose();
			Disposables = null;

			return base.OnClose();
		}

		private async Task DoDequeueAsync(IEnumerable<CoinViewModel> selectedCoins)
		{
			IsDequeueBusy = true;
			try
			{
				SetWarningMessage("");

				if (!selectedCoins.Any())
				{
					SetWarningMessage("No coins are selected to dequeue.");
					return;
				}

				try
				{
					await Global.ChaumianClient.DequeueCoinsFromMixAsync(selectedCoins.Select(c => c.Model).ToArray(), "Dequeued by the user.");
				}
				catch (Exception ex)
				{
					Logger.LogWarning(ex);
					var builder = new StringBuilder(ex.ToTypeMessageString());
					if (ex is AggregateException aggex)
					{
						foreach (var iex in aggex.InnerExceptions)
						{
							builder.Append(Environment.NewLine + iex.ToTypeMessageString());
						}
					}
					SetWarningMessage(builder.ToString());
				}
			}
			finally
			{
				IsDequeueBusy = false;
			}
		}

		private async Task DoEnqueueAsync(IEnumerable<CoinViewModel> selectedCoins)
		{
			IsEnqueueBusy = true;
			try
			{
				SetWarningMessage("");

				if (!selectedCoins.Any())
				{
					SetWarningMessage("No coins are selected to enqueue.");
					return;
				}
				try
				{
					PasswordHelper.GetMasterExtKey(KeyManager, Password, out string compatiblityPassword); // If the password is not correct we throw.

					if (compatiblityPassword != null)
					{
						Password = compatiblityPassword;
						SetWarningMessage(PasswordHelper.CompatibilityPasswordWarnMessage);
					}

					await Global.ChaumianClient.QueueCoinsToMixAsync(Password, selectedCoins.Select(c => c.Model).ToArray());
				}
				catch (Exception ex)
				{
					Logger.LogWarning(ex);
					var builder = new StringBuilder(ex.ToTypeMessageString());
					if (ex is AggregateException aggex)
					{
						foreach (var iex in aggex.InnerExceptions)
						{
							builder.Append(Environment.NewLine + iex.ToTypeMessageString());
						}
					}
					SetWarningMessage(builder.ToString());
				}

				Password = string.Empty;
			}
			finally
			{
				IsEnqueueBusy = false;
			}
		}

		private void UpdateStates()
		{
			var chaumianClient = Global?.ChaumianClient;
			if (chaumianClient is null)
			{
				return;
			}

			AmountQueued = chaumianClient.State.SumAllQueuedCoinAmounts();
			MainWindowViewModel.Instance.CanClose = AmountQueued == Money.Zero;

			var registrableRound = chaumianClient.State.GetRegistrableRoundOrDefault();
			if (registrableRound != default)
			{
				CoordinatorFeePercent = registrableRound.State.CoordinatorFeePercent.ToString();
				UpdateRequiredBtcLabel(registrableRound);
			}
			var mostAdvancedRound = chaumianClient.State.GetMostAdvancedRoundOrDefault();
			if (mostAdvancedRound != default)
			{
				RoundId = mostAdvancedRound.State.RoundId;
				if (!chaumianClient.State.IsInErrorState)
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

		private void UpdateRequiredBtcLabel(CcjClientRound registrableRound)
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
					RequiredBTC = available.Any()
						? registrableRound.State.CalculateRequiredAmount(available.Where(x => x.AnonymitySet < Global.Config.PrivacyLevelStrong).Select(x => x.Amount).ToArray())
						: registrableRound.State.CalculateRequiredAmount();
				}
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

		public ErrorDescriptors ValidatePassword() => PasswordHelper.ValidatePassword(Password);

		[ValidateMethod(nameof(ValidatePassword))]
		public string Password
		{
			get => _password;
			set => this.RaiseAndSetIfChanged(ref _password, value);
		}

		public CoinListViewModel CoinsList { get; }

		private async void OnCoinsListDequeueCoinsPressedAsync()
		{
			try
			{
				var selectedCoin = CoinsList.SelectedCoin;
				if (selectedCoin is null)
				{
					return;
				}

				await DoDequeueAsync(new[] { selectedCoin });
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}
		}

		public Money AmountQueued
		{
			get => _amountQueued;
			set => this.RaiseAndSetIfChanged(ref _amountQueued, value);
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

		public bool IsEnqueueBusy
		{
			get => _isEnqueueBusy;
			set => this.RaiseAndSetIfChanged(ref _isEnqueueBusy, value);
		}

		public bool IsDequeueBusy
		{
			get => _isDequeueBusy;
			set => this.RaiseAndSetIfChanged(ref _isDequeueBusy, value);
		}

		public string EnqueueButtonText
		{
			get => _enqueueButtonText;
			set => this.RaiseAndSetIfChanged(ref _enqueueButtonText, value);
		}

		public string DequeueButtonText
		{
			get => _dequeueButtonText;
			set => this.RaiseAndSetIfChanged(ref _dequeueButtonText, value);
		}

		public int CoinJoinUntilAnonymitySet
		{
			get => _coinJoinUntilAnonymitySet;
			set => this.RaiseAndSetIfChanged(ref _coinJoinUntilAnonymitySet, value);
		}

		private TargetPrivacy TargetPrivacy
		{
			get => _targetPrivacy;
			set => this.RaiseAndSetIfChanged(ref _targetPrivacy, value);
		}

		public bool IsLurkingWifeMode => Global.UiConfig.LurkingWifeMode is true;

		public ReactiveCommand<Unit, Unit> EnqueueCommand { get; }

		public ReactiveCommand<Unit, Unit> DequeueCommand { get; }

		public ReactiveCommand<Unit, TargetPrivacy> PrivacySomeCommand { get; }
		public ReactiveCommand<Unit, TargetPrivacy> PrivacyFineCommand { get; }
		public ReactiveCommand<Unit, TargetPrivacy> PrivacyStrongCommand { get; }
		public ReactiveCommand<Unit, Unit> TargetButtonCommand { get; }
	}
}
