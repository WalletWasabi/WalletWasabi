using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.State;
using WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.CoinJoinProgressEvents;
using WalletWasabi.WabiSabi.Client.StatusChangedEvents;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public partial class CoinJoinStateViewModel : ViewModelBase
{
	private readonly StateMachine<State, Trigger> _stateMachine;
	private readonly Wallet _wallet;
	private readonly DispatcherTimer _countdownTimer;

	private readonly MusicStatusMessageViewModel _countDownMessage = new() { Message = "Waiting to auto-start coinjoin" };
	private readonly MusicStatusMessageViewModel _waitingMessage = new() { Message = "Waiting for coinjoin" };
	private readonly MusicStatusMessageViewModel _pauseMessage = new() { Message = "Coinjoin is paused" };
	private readonly MusicStatusMessageViewModel _stoppedMessage = new() { Message = "Coinjoin is stopped" };
	private readonly MusicStatusMessageViewModel _initialisingMessage = new() { Message = "Coinjoin is initialising" };
	private readonly MusicStatusMessageViewModel _finishedMessage = new() { Message = "Not enough non-private funds to coinjoin" };
	private readonly MusicStatusMessageViewModel _roundSucceedMessage = new() { Message = "Successful coinjoin" };
	private readonly MusicStatusMessageViewModel _roundFailedMessage = new() { Message = "Coinjoin failed, retrying..." };
	private readonly MusicStatusMessageViewModel _outputRegistrationMessage = new() { Message = "Constructing coinjoin" };
	private readonly MusicStatusMessageViewModel _inputRegistrationMessage = new() { Message = "Waiting for others" };
	private readonly MusicStatusMessageViewModel _transactionSigningMessage = new() { Message = "Finalizing coinjoin" };
	private readonly MusicStatusMessageViewModel _waitingForBlameRoundMessage = new() { Message = "Waiting for the fallback round" };
	private readonly MusicStatusMessageViewModel _waitingRoundMessage = new() { Message = "Waiting for a round" };
	private readonly MusicStatusMessageViewModel _connectionConfirmationMessage = new() { Message = "Preparing coinjoin" };
	private readonly MusicStatusMessageViewModel _plebStopMessage = new() { Message = "Coinjoining might be uneconomical" };
	private readonly MusicStatusMessageViewModel _plebStopMessageBelow = new() { Message = "Receive more funds or press play to bypass" };

	[AutoNotify] private bool _isAutoWaiting;
	[AutoNotify] private bool _isAuto;
	[AutoNotify] private bool _playVisible = true;
	[AutoNotify] private bool _pauseVisible;
	[AutoNotify] private bool _stopVisible;
	[AutoNotify] private MusicStatusMessageViewModel? _currentStatus;
	[AutoNotify] private bool _isProgressReversed;
	[AutoNotify] private double _progressValue;
	[AutoNotify] private string _elapsedTime;
	[AutoNotify] private string _remainingTime;
	[AutoNotify] private bool _isBalanceDisplayed;
	[AutoNotify] private bool _isInCriticalPhase;
	[AutoNotify] private bool _isCountDownDelayHappening;

	private DateTimeOffset _countDownStartTime;
	private DateTimeOffset _countDownEndTime;
	private bool _overridePlebStop;
	private CoinJoinProgressEventArgs? _lastStatusMessage;

	public CoinJoinStateViewModel(WalletViewModel walletVm, IObservable<Unit> balanceChanged)
	{
		_wallet = walletVm.Wallet;

		_elapsedTime = "";
		_remainingTime = "";

		_countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
		_countdownTimer.Tick += OnTimerTick;

		var coinJoinManager = Services.HostedServices.Get<CoinJoinManager>();

		Observable.FromEventPattern<StatusChangedEventArgs>(coinJoinManager, nameof(coinJoinManager.StatusChanged))
			.Where(x => x.EventArgs.Wallet == walletVm.Wallet)
			.Select(x => x.EventArgs)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(StatusChanged);

		this.WhenAnyValue(x => x.RemainingTime)
			.Subscribe(text => IsBalanceDisplayed = text.Contains("BTC"));

		var initialState = walletVm.Settings.AutoCoinJoin
			? State.AutoCoinJoin
			: State.ManualCoinJoin;

		if (walletVm.Wallet.KeyManager.IsHardwareWallet || walletVm.Wallet.KeyManager.IsWatchOnly)
		{
			initialState = State.Disabled;
		}

		_stateMachine = new StateMachine<State, Trigger>(initialState);

		ConfigureStateMachine(coinJoinManager);

		balanceChanged.Subscribe(_ => _stateMachine.Fire(Trigger.BalanceChanged));

		PlayCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			if (!_wallet.KeyManager.IsCoinjoinProfileSelected)
			{
				await RoutableViewModel.NavigateDialogAsync(new CoinJoinProfilesViewModel(_wallet.KeyManager, isNewWallet: false), NavigationTarget.DialogScreen);
			}

			if (_wallet.KeyManager.IsCoinjoinProfileSelected)
			{
				_stateMachine.Fire(Trigger.Play);
			}
		});

		PauseCommand = ReactiveCommand.Create(() => _stateMachine.Fire(Trigger.Pause));

		StopCommand = ReactiveCommand.Create(() => _stateMachine.Fire(Trigger.Stop));

		walletVm.Settings.WhenAnyValue(x => x.AutoCoinJoin)
			.Subscribe(SetAutoCoinJoin);

		walletVm.Settings.WhenAnyValue(x => x.PlebStopThreshold)
			.SubscribeAsync(async _ =>
			{
				// Hack: we take the value from KeyManager but it is saved later.
				// https://github.com/molnard/WalletWasabi/blob/master/WalletWasabi.Fluent/ViewModels/Wallets/WalletSettingsViewModel.cs#L105
				await Task.Delay(1500);
				_stateMachine.Fire(Trigger.PlebStopChanged);
			});

		_stateMachine.Start();
	}

	private enum State
	{
		Invalid = 0,
		Disabled,
		AutoCoinJoin,
		ManualCoinJoin,

		AutoStarting,
		Paused,
		AutoPlaying,
		AutoPlayingCritical,
		AutoFinished,
		AutoFinishedPlebStop,

		Stopped,
		ManualPlaying,
		ManualPlayingCritical,
		ManualFinished,

		ManualFinishedPlebStop,
	}

	private enum Trigger
	{
		Invalid = 0,
		AutoStartTimeout,
		AutoCoinJoinOn,
		AutoCoinJoinOff,
		Pause,
		Play,
		Stop,
		PlebStop,
		RoundStartFailed,
		RoundStart,
		RoundFinished,
		BalanceChanged,
		Timer,
		PlebStopChanged,
		RoundEndedMessage,
		OutputRegistrationMessage,
		SigningPhaseMessage,
		InputRegistrationMessage,
		WaitingForBlameRoundMessage,
		WaitingForRoundMessage,
		ConnectionConfirmationPhaseMessage,
		EnterCriticalPhaseMessage,
		ExitCriticalPhaseMessage,
		AllCoinsPrivate,
	}

	private bool IsCountDownFinished => GetRemainingTime() <= TimeSpan.Zero;

	private bool IsCounting => _countdownTimer.IsEnabled;

	public ICommand PlayCommand { get; }

	public ICommand PauseCommand { get; }

	public ICommand StopCommand { get; }

	private void ConfigureStateMachine(CoinJoinManager coinJoinManager)
	{
		// See diagram in the developer docs.
		_stateMachine.Configure(State.Disabled);

		// Manual Cj State
		_stateMachine.Configure(State.ManualCoinJoin)
			.Permit(Trigger.AutoCoinJoinOn, State.AutoCoinJoin)
			.InitialTransition(State.Stopped)
			.OnEntry(() =>
			{
				IsAuto = false;
				IsAutoWaiting = false;
				PlayVisible = true;
				StopVisible = false;
				PauseVisible = false;
			})
			.OnEntry(UpdateAndShowWalletMixedProgress)
			.OnTrigger(Trigger.BalanceChanged, UpdateAndShowWalletMixedProgress);

		_stateMachine.Configure(State.Stopped)
			.SubstateOf(State.ManualCoinJoin)
			.Permit(Trigger.Play, State.ManualPlaying)
			.OnEntry(async () =>
			{
				ProgressValue = 0;
				ElapsedTime = "";
				RemainingTime = "";
				StopVisible = false;
				PlayVisible = true;
				_wallet.AllowManualCoinJoin = false;
				CurrentStatus = _stoppedMessage;

				await coinJoinManager.StopAsync(_wallet, CancellationToken.None);
			})
			.OnEntry(UpdateAndShowWalletMixedProgress)
			.OnTrigger(Trigger.BalanceChanged, UpdateAndShowWalletMixedProgress)
			.OnTrigger(Trigger.Play, async () =>
			{
				await coinJoinManager.StartAsync(_wallet, stopWhenMixed: true, _overridePlebStop, CancellationToken.None);
			});

		_stateMachine.Configure(State.ManualPlaying)
			.SubstateOf(State.ManualCoinJoin)
			.Permit(Trigger.Stop, State.Stopped)
			.Permit(Trigger.AllCoinsPrivate, State.ManualFinished)
			.Permit(Trigger.PlebStop, State.ManualFinishedPlebStop)
			.Permit(Trigger.EnterCriticalPhaseMessage, State.ManualPlayingCritical)
			.OnEntry(async () =>
			{
				PlayVisible = false;
				StopVisible = true;
				CurrentStatus = _waitingMessage;

				if (_overridePlebStop && !_wallet.IsUnderPlebStop)
				{
					// If we are not below the threshold anymore, we turn off the override.
					_overridePlebStop = false;
					await coinJoinManager.StartAsync(_wallet, stopWhenMixed: true, _overridePlebStop, CancellationToken.None);
				}
			})
			.Custom(HandleMessages)
			.OnEntry(UpdateAndShowWalletMixedProgress)
			.OnTrigger(Trigger.BalanceChanged, UpdateAndShowWalletMixedProgress)
			.OnTrigger(Trigger.Timer, UpdateCountDown)
			.OnTrigger(Trigger.Stop, () => _overridePlebStop = false);

		_stateMachine.Configure(State.ManualPlayingCritical)
			.SubstateOf(State.ManualPlaying)
			.Permit(Trigger.ExitCriticalPhaseMessage, State.ManualPlaying)
			.OnEntry(() =>
			{
				StopVisible = false;
			})
			.OnExit(() =>
			{
				StopVisible = true;
			});

		_stateMachine.Configure(State.ManualFinished)
			.SubstateOf(State.ManualCoinJoin)
			.Permit(Trigger.Play, State.ManualPlaying)
			.OnEntry(() =>
			{
				StopVisible = false;
				PlayVisible = true;
				CurrentStatus = _finishedMessage;
				ProgressValue = 100;
				ElapsedTime = "";
				RemainingTime = "";
			});

		_stateMachine.Configure(State.ManualFinishedPlebStop)
			.SubstateOf(State.ManualFinished)
			.Permit(Trigger.BalanceChanged, State.ManualPlaying)
			.Permit(Trigger.PlebStopChanged, State.ManualPlaying)
			.Permit(Trigger.Stop, State.Stopped)
			.OnEntry(() =>
			{
				CurrentStatus = _plebStopMessage;
				ElapsedTime = _plebStopMessageBelow.Message ?? "";
				ProgressValue = 0;

				StopVisible = true;
				PauseVisible = false;
				PlayVisible = true;
			})
			.OnTrigger(Trigger.Play, async () =>
			{
				_overridePlebStop = true;
				await coinJoinManager.StartAsync(_wallet, stopWhenMixed: true, _overridePlebStop, CancellationToken.None);
			});

		// AutoCj State
		_stateMachine.Configure(State.AutoCoinJoin)
			.InitialTransition(State.AutoStarting)
			.Permit(Trigger.AutoCoinJoinOff, State.ManualCoinJoin)
			.OnEntry(async () =>
			{
				IsAuto = true;
				StopVisible = false;
				PauseVisible = false;
				PlayVisible = false;

				CurrentStatus = _initialisingMessage;

				await coinJoinManager.StopAsync(_wallet, CancellationToken.None);
			});

		_stateMachine.Configure(State.AutoStarting)
			.SubstateOf(State.AutoCoinJoin)
			.Permit(Trigger.Pause, State.Paused)
			.Permit(Trigger.RoundStart, State.AutoPlaying)
			.Permit(Trigger.AutoStartTimeout, State.AutoPlaying)
			.Permit(Trigger.Play, State.AutoPlaying)
			.Permit(Trigger.RoundStartFailed, State.AutoFinished)
			.OnEntry(() =>
			{
				PlayVisible = true;
				IsAutoWaiting = true;
				var now = DateTimeOffset.UtcNow;
				StartCountDown(_countDownMessage, start: now, end: now + TimeSpan.FromSeconds(Random.Shared.Next(5 * 60, 16 * 60)));
			})
			.OnTrigger(Trigger.Timer, UpdateAutoStartCountDown)
			.OnExit(() =>
			{
				StopCountDown();
				IsAutoWaiting = false;
			});

		_stateMachine.Configure(State.Paused)
			.SubstateOf(State.AutoCoinJoin)
			.Permit(Trigger.Play, State.AutoPlaying)
			.OnEntry(async () =>
			{
				IsAutoWaiting = true;

				CurrentStatus = _pauseMessage;
				ProgressValue = 0;
				ElapsedTime = "";
				RemainingTime = "";

				PauseVisible = false;
				PlayVisible = true;

				await coinJoinManager.StopAsync(_wallet, CancellationToken.None);
			})
			.OnEntry(UpdateAndShowWalletMixedProgress)
			.OnTrigger(Trigger.BalanceChanged, UpdateAndShowWalletMixedProgress);

		_stateMachine.Configure(State.AutoPlaying)
			.SubstateOf(State.AutoCoinJoin)
			.Permit(Trigger.Pause, State.Paused)
			.Permit(Trigger.PlebStop, State.AutoFinishedPlebStop)
			.Permit(Trigger.RoundStartFailed, State.AutoFinished)
			.Permit(Trigger.EnterCriticalPhaseMessage, State.AutoPlayingCritical)
			.OnEntry(async () =>
			{
				CurrentStatus = _waitingMessage;
				IsAutoWaiting = false;
				PauseVisible = true;
				PlayVisible = false;

				if (_overridePlebStop && !_wallet.IsUnderPlebStop)
				{
					// If we are not below the threshold anymore, we turn off the override.
					_overridePlebStop = false;
				}

				await coinJoinManager.StartAsync(_wallet, stopWhenMixed: false, _overridePlebStop, CancellationToken.None);
			})
			.Custom(HandleMessages)
			.OnEntry(UpdateAndShowWalletMixedProgress)
			.OnTrigger(Trigger.BalanceChanged, UpdateAndShowWalletMixedProgress)
			.OnTrigger(Trigger.Timer, UpdateCountDown)
			.OnTrigger(Trigger.Pause, () => _overridePlebStop = false);

		_stateMachine.Configure(State.AutoPlayingCritical)
			.SubstateOf(State.AutoPlaying)
			.Permit(Trigger.ExitCriticalPhaseMessage, State.AutoPlaying)
			.OnEntry(() =>
			{
				PauseVisible = false;
			})
			.OnExit(() =>
			{
				PauseVisible = true;
			});

		_stateMachine.Configure(State.AutoFinished)
			.SubstateOf(State.AutoCoinJoin)
			.Permit(Trigger.RoundStart, State.AutoPlaying)
			.Permit(Trigger.BalanceChanged, State.AutoPlaying)
			.OnEntry(() =>
			{
				PauseVisible = false;
				PlayVisible = false;

				ProgressValue = 100;
				ElapsedTime = "";
				RemainingTime = "";

				CurrentStatus = _finishedMessage;
			});

		_stateMachine.Configure(State.AutoFinishedPlebStop)
			.SubstateOf(State.AutoFinished)
			.Permit(Trigger.Play, State.AutoPlaying)
			.Permit(Trigger.PlebStopChanged, State.AutoPlaying)
			.Permit(Trigger.Pause, State.Paused)
			.OnEntry(() =>
			{
				CurrentStatus = _plebStopMessage;
				ElapsedTime = _plebStopMessageBelow.Message ?? "";
				ProgressValue = 0;

				StopVisible = false;
				PauseVisible = true;
				PlayVisible = true;
			})
			.OnTrigger(Trigger.Play, () =>
			{
				_overridePlebStop = true;
			});
	}

	private void UpdateAutoStartCountDown()
	{
		UpdateCountDown();

		if (IsCountDownFinished)
		{
			_stateMachine.Fire(Trigger.AutoStartTimeout);
		}
	}

	private void UpdateCountDown()
	{
		IsCountDownDelayHappening = IsCounting && IsCountDownFinished;

		// This case mostly happens when there is some delay between the client and the server,
		// and the countdown has finished but the client hasn't received any new phase changed message.
		if (IsCountDownDelayHappening)
		{
			ElapsedTime = "Waiting for response";
			RemainingTime = "";
			return;
		}

		var format = @"hh\:mm\:ss";
		ElapsedTime = $"{GetElapsedTime().ToString(format)}";
		RemainingTime = $"-{GetRemainingTime().ToString(format)}";
		ProgressValue = GetPercentage();
	}

	private TimeSpan GetElapsedTime() => DateTimeOffset.Now - _countDownStartTime;

	private TimeSpan GetRemainingTime() => _countDownEndTime - DateTimeOffset.Now;

	private TimeSpan GetTotalTime() => _countDownEndTime - _countDownStartTime;

	private double GetPercentage() => GetElapsedTime().TotalSeconds / GetTotalTime().TotalSeconds * 100;

	private void UpdateAndShowWalletMixedProgress()
	{
		if (!_wallet.Coins.Any() || IsCounting)
		{
			return;
		}

		var privateThreshold = _wallet.KeyManager.AnonScoreTarget;

		var privateAmount = _wallet.Coins.FilterBy(x => x.HdPubKey.AnonymitySet >= privateThreshold).TotalAmount();
		var normalAmount = _wallet.Coins.FilterBy(x => x.HdPubKey.AnonymitySet < privateThreshold).TotalAmount();
		var total = _wallet.Coins.TotalAmount();

		ElapsedTime = "Balance to coinjoin:";
		RemainingTime = normalAmount.ToFormattedString() + " BTC";

		var percentage = privateAmount.ToDecimal(MoneyUnit.BTC) / total.ToDecimal(MoneyUnit.BTC) * 100;

		ProgressValue = (double)percentage;
	}

	private void StatusChanged(StatusChangedEventArgs e)
	{
		switch (e)
		{
			case CompletedEventArgs:
				_stateMachine.Fire(Trigger.RoundFinished);
				break;

			case StartedEventArgs:
				_stateMachine.Fire(Trigger.RoundStart);
				break;

			case StartErrorEventArgs start when start.Error is CoinjoinError.NotEnoughUnprivateBalance:
				_stateMachine.Fire(Trigger.PlebStop);
				break;

			case StartErrorEventArgs start when start.Error is CoinjoinError.AllCoinsPrivate:
				_stateMachine.Fire(Trigger.AllCoinsPrivate);
				break;

			case StartErrorEventArgs:
				_stateMachine.Fire(Trigger.RoundStartFailed);
				break;

			case CoinJoinStatusEventArgs coinJoinStatusEventArgs when coinJoinStatusEventArgs.Wallet == _wallet:
				OnCoinJoinPhaseChanged(coinJoinStatusEventArgs.CoinJoinProgressEventArgs);
				break;
		}
	}

	private void OnCoinJoinPhaseChanged(CoinJoinProgressEventArgs coinJoinProgress)
	{
		switch (coinJoinProgress)
		{
			case RoundEnded roundEnded:
				_lastStatusMessage = roundEnded;
				_stateMachine.Fire(Trigger.RoundEndedMessage);
				break;

			case EnteringOutputRegistrationPhase enteringOutputRegistrationPhase:
				_lastStatusMessage = enteringOutputRegistrationPhase;
				_stateMachine.Fire(Trigger.OutputRegistrationMessage);
				break;

			case EnteringSigningPhase enteringSigningPhase:
				_lastStatusMessage = enteringSigningPhase;
				_stateMachine.Fire(Trigger.SigningPhaseMessage);
				break;

			case EnteringInputRegistrationPhase enteringInputRegistrationPhase:
				_lastStatusMessage = enteringInputRegistrationPhase;
				_stateMachine.Fire(Trigger.InputRegistrationMessage);
				break;

			case WaitingForBlameRound waitingForBlameRound:
				_lastStatusMessage = waitingForBlameRound;
				_stateMachine.Fire(Trigger.WaitingForBlameRoundMessage);
				break;

			case WaitingForRound:
				_stateMachine.Fire(Trigger.WaitingForRoundMessage);
				break;

			case EnteringConnectionConfirmationPhase:
				_lastStatusMessage = coinJoinProgress;
				_stateMachine.Fire(Trigger.ConnectionConfirmationPhaseMessage);
				break;

			case EnteringCriticalPhase:
				_stateMachine.Fire(Trigger.EnterCriticalPhaseMessage);
				IsInCriticalPhase = true;
				break;

			case LeavingCriticalPhase:
				_stateMachine.Fire(Trigger.ExitCriticalPhaseMessage);
				IsInCriticalPhase = false;
				break;
		}
	}

	private StateMachine<State, Trigger>.StateContext HandleMessages(StateMachine<State, Trigger>.StateContext context)
	{
		return context.OnTrigger(Trigger.RoundEndedMessage, () =>
			{
				if (_lastStatusMessage is RoundEnded roundEnded)
				{
					CurrentStatus = roundEnded.LastRoundState.EndRoundState == EndRoundState.TransactionBroadcasted
						? _roundSucceedMessage
						: _roundFailedMessage;
					StopCountDown();
				}
			})
			.OnTrigger(Trigger.InputRegistrationMessage, () =>
			{
				if (_lastStatusMessage is EnteringInputRegistrationPhase enteringInputRegistrationPhase)
				{
					StartCountDown(
						message: _inputRegistrationMessage,
						start: enteringInputRegistrationPhase.TimeoutAt - enteringInputRegistrationPhase.RoundState
							.CoinjoinState.Parameters.StandardInputRegistrationTimeout,
						end: enteringInputRegistrationPhase.TimeoutAt);

					_lastStatusMessage = null;
				}
			})
			.OnTrigger(Trigger.OutputRegistrationMessage, () =>
			{
				if (_lastStatusMessage is EnteringOutputRegistrationPhase enteringOutputRegistrationPhase)
				{
					StartCountDown(
						message: _outputRegistrationMessage,
						start: enteringOutputRegistrationPhase.TimeoutAt - enteringOutputRegistrationPhase.RoundState
							.CoinjoinState.Parameters.OutputRegistrationTimeout,
						end: enteringOutputRegistrationPhase.TimeoutAt);

					_lastStatusMessage = null;
				}
			})
			.OnTrigger(Trigger.SigningPhaseMessage, () =>
			{
				if (_lastStatusMessage is EnteringSigningPhase enteringSigningPhase)
				{
					StartCountDown(
						message: _transactionSigningMessage,
						start: enteringSigningPhase.TimeoutAt - enteringSigningPhase.RoundState.CoinjoinState.Parameters
							.TransactionSigningTimeout,
						end: enteringSigningPhase.TimeoutAt);

					_lastStatusMessage = null;
				}
			})
			.OnTrigger(Trigger.ConnectionConfirmationPhaseMessage, () =>
			{
				if (_lastStatusMessage is EnteringConnectionConfirmationPhase confirmationPhaseArgs)
				{
					StartCountDown(
						message: _connectionConfirmationMessage,
						start: confirmationPhaseArgs.TimeoutAt - confirmationPhaseArgs
							.RoundState.CoinjoinState.Parameters.ConnectionConfirmationTimeout,
						end: confirmationPhaseArgs.TimeoutAt);

					_lastStatusMessage = null;
				}
			})
			.OnTrigger(Trigger.WaitingForBlameRoundMessage, () =>
			{
				if (_lastStatusMessage is WaitingForBlameRound waitingForBlameRound)
				{
					StartCountDown(message: _waitingForBlameRoundMessage, start: DateTimeOffset.UtcNow,
						end: waitingForBlameRound.TimeoutAt);

					_lastStatusMessage = null;
				}
			})
			.OnTrigger(Trigger.WaitingForRoundMessage, () =>
			{
				CurrentStatus = _waitingRoundMessage;
				StopCountDown();
			});
	}

	private void StartCountDown(MusicStatusMessageViewModel message, DateTimeOffset start, DateTimeOffset end)
	{
		CurrentStatus = message;
		_countDownStartTime = start;
		_countDownEndTime = end;
		UpdateCountDown(); // force the UI to apply the changes at the same time.
		_countdownTimer.Start();
	}

	private void StopCountDown()
	{
		_countdownTimer.Stop();
		IsCountDownDelayHappening = false;
		_countDownStartTime = DateTimeOffset.MinValue;
		_countDownEndTime = DateTimeOffset.MinValue;
		UpdateAndShowWalletMixedProgress();
	}

	private void OnTimerTick(object? sender, EventArgs e)
	{
		_stateMachine.Fire(Trigger.Timer);
	}

	private void SetAutoCoinJoin(bool enabled)
	{
		_stateMachine.Fire(enabled ? Trigger.AutoCoinJoinOn : Trigger.AutoCoinJoinOff);
	}
}
