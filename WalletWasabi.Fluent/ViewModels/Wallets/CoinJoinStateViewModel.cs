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
	private readonly DispatcherTimer _autoCoinJoinStartTimer;

	private readonly MusicStatusMessageViewModel _countDownMessage = new() { Message = "Waiting to auto-start coinjoin" };
	private readonly MusicStatusMessageViewModel _waitingMessage = new() { Message = "Waiting for coinjoin" };
	private readonly MusicStatusMessageViewModel _pauseMessage = new() { Message = "Coinjoin is paused" };
	private readonly MusicStatusMessageViewModel _stoppedMessage = new() { Message = "Coinjoin is stopped" };
	private readonly MusicStatusMessageViewModel _initialisingMessage = new() { Message = "Coinjoin is initialising" };
	private readonly MusicStatusMessageViewModel _finishedMessage = new() { Message = "Not enough non-private funds to coinjoin" };
	private readonly MusicStatusMessageViewModel _roundSucceedMessage = new() { Message = "Successful coinjoin" };
	private readonly MusicStatusMessageViewModel _roundFailedMessage = new() { Message = "Coinjoin failed, retrying..." };
	private readonly MusicStatusMessageViewModel _abortedNotEnoughAlicesMessage = new() { Message = "Not enough participants, retrying..." };
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
	private CoinJoinProgressEventArgs? _lastStatusMessage;

	public bool IsAutoCoinJoinEnabled => WalletVm.Settings.AutoCoinJoin;

	public CoinJoinStateViewModel(WalletViewModel walletVm, IObservable<Unit> balanceChanged)
	{
		_wallet = walletVm.Wallet;
		WalletVm = walletVm;

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
			? State.WaitingForAutoStart
			: State.StopOrPause;

		if (walletVm.Wallet.KeyManager.IsHardwareWallet || walletVm.Wallet.KeyManager.IsWatchOnly)
		{
			initialState = State.Disabled;
		}

		_stateMachine = new StateMachine<State, Trigger>(initialState);

		ConfigureStateMachine();

		balanceChanged.Subscribe(_ => _stateMachine.Fire(Trigger.BalanceChanged));

		PlayCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			if (!_wallet.KeyManager.IsCoinjoinProfileSelected)
			{
				await RoutableViewModel.NavigateDialogAsync(new CoinJoinProfilesViewModel(_wallet.KeyManager, isNewWallet: false), NavigationTarget.DialogScreen);
			}

			if (_wallet.KeyManager.IsCoinjoinProfileSelected)
			{
				var overridePlebStop = _stateMachine.IsInState(State.PlebStopActive);
				await coinJoinManager.StartAsync(_wallet, stopWhenAllMixed: !IsAutoCoinJoinEnabled, overridePlebStop, CancellationToken.None);
			}
		});

		StopPauseCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			await coinJoinManager.StopAsync(_wallet, CancellationToken.None);
		});

		walletVm.Settings.WhenAnyValue(x => x.AutoCoinJoin)
			.SubscribeAsync(async (autoCoinJoin) =>
			{
				if (autoCoinJoin)
				{
					await coinJoinManager.StartAsync(_wallet, stopWhenAllMixed: false, false, CancellationToken.None);
				}
				else
				{
					await coinJoinManager.StopAsync(_wallet, CancellationToken.None);
				}
			});

		walletVm.Settings.WhenAnyValue(x => x.PlebStopThreshold)
			.SubscribeAsync(async _ =>
			{
				// Hack: we take the value from KeyManager but it is saved later.
				// https://github.com/molnard/WalletWasabi/blob/master/WalletWasabi.Fluent/ViewModels/Wallets/WalletSettingsViewModel.cs#L105
				await Task.Delay(1500);
				_stateMachine.Fire(Trigger.PlebStopChanged);
			});

		_autoCoinJoinStartTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(Random.Shared.Next(5, 16)) };
		_autoCoinJoinStartTimer.Tick += async (_, _) =>
		{
			await coinJoinManager.StartAsync(_wallet, stopWhenAllMixed: false, false, CancellationToken.None);
			_autoCoinJoinStartTimer.Stop();
		};

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

		StopOrPause,
		ManualPlaying,
		ManualPlayingCritical,
		ManualFinished,

		ManualFinishedPlebStop,
		WaitingForAutoStart,
		Playing,
		PlebStopActive,
	}

	private enum Trigger
	{
		Invalid = 0,
		PlebStopActivated,
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
		WalletStartedCoinJoin,
		WalletStoppedCoinJoin
	}

	private bool IsCountDownFinished => GetRemainingTime() <= TimeSpan.Zero;

	private bool IsCounting => _countdownTimer.IsEnabled;

	public ICommand PlayCommand { get; }

	public ICommand StopPauseCommand { get; }

	public WalletViewModel WalletVm { get; }

	private void ConfigureStateMachine()
	{
		// See diagram in the developer docs.
		_stateMachine.Configure(State.Disabled);

		var autoStartEnd = DateTimeOffset.UtcNow;

		_stateMachine.Configure(State.WaitingForAutoStart)
			.Permit(Trigger.WalletStartedCoinJoin, State.Playing)
			.OnEntry(() =>
			{
				PlayVisible = true;
				PauseVisible = false;
				StopVisible = false;

				var now = DateTimeOffset.UtcNow;
				autoStartEnd = now + _autoCoinJoinStartTimer.Interval;
				_autoCoinJoinStartTimer.Start();

				StartCountDown(_countDownMessage, now, autoStartEnd);
			})
			.OnExit(() =>
			{
				_autoCoinJoinStartTimer.Stop();
				StopCountDown();
			})
			.OnTrigger(Trigger.Timer, () =>
			{
				UpdateCountDown();
			});

		_stateMachine.Configure(State.StopOrPause)
			.Permit(Trigger.WalletStartedCoinJoin, State.Playing)
			.Permit(Trigger.PlebStopActivated, State.PlebStopActive)
			.OnEntry(() =>
			{
				PlayVisible = true;
				PauseVisible = false;
				StopVisible = false;

				CurrentStatus = IsAutoCoinJoinEnabled ? _pauseMessage : _stoppedMessage;
				ElapsedTime = "Press Play to start";
			})
			.OnExit(() =>
			{
				ElapsedTime = "";
			});

		_stateMachine.Configure(State.Playing)
			.Permit(Trigger.WalletStoppedCoinJoin, State.StopOrPause)
			.Permit(Trigger.PlebStopActivated, State.PlebStopActive)
			.OnEntry(() =>
			{
				PlayVisible = false;
				PauseVisible = IsAutoCoinJoinEnabled;
				StopVisible = !IsAutoCoinJoinEnabled;

				CurrentStatus = _waitingMessage;
			})
			.OnTrigger(Trigger.EnterCriticalPhaseMessage, () =>
			{
				PauseVisible = false;
				StopVisible = false;
			})
			.OnTrigger(Trigger.ExitCriticalPhaseMessage, () =>
			{
				PauseVisible = IsAutoCoinJoinEnabled;
				StopVisible = !IsAutoCoinJoinEnabled;
			});

		_stateMachine.Configure(State.PlebStopActive)
			.Permit(Trigger.BalanceChanged, State.Playing)
			.Permit(Trigger.PlebStopChanged, State.Playing)
			.Permit(Trigger.WalletStartedCoinJoin, State.Playing)
			.Permit(Trigger.WalletStoppedCoinJoin, State.StopOrPause)
			.OnEntry(() =>
			{
				PlayVisible = true;
				PauseVisible = false;
				StopVisible = false;

				CurrentStatus = _plebStopMessage;
				ElapsedTime = _plebStopMessageBelow.Message!;
			})
			.OnExit(() =>
			{
				ElapsedTime = "";
			});
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

	private TimeSpan GetElapsedTime() => DateTimeOffset.UtcNow - _countDownStartTime;

	private TimeSpan GetRemainingTime() => _countDownEndTime - DateTimeOffset.UtcNow;

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
			case WalletStartedCoinJoinEventArgs:
				_stateMachine.Fire(Trigger.WalletStartedCoinJoin);
				break;

			case WalletStoppedCoinJoinEventArgs:
				_stateMachine.Fire(Trigger.WalletStoppedCoinJoin);
				break;

			case CompletedEventArgs:
				_stateMachine.Fire(Trigger.RoundFinished);
				break;

			case StartedEventArgs:
				_stateMachine.Fire(Trigger.RoundStart);
				break;

			case StartErrorEventArgs start when start.Error is CoinjoinError.NotEnoughUnprivateBalance:
				_stateMachine.Fire(Trigger.PlebStopActivated);
				break;

			case StartErrorEventArgs:
				_stateMachine.Fire(Trigger.RoundStartFailed);
				break;

			case CoinJoinStatusEventArgs coinJoinStatusEventArgs:
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
					CurrentStatus = roundEnded.LastRoundState.EndRoundState switch
					{
						EndRoundState.TransactionBroadcasted => _roundSucceedMessage,
						EndRoundState.AbortedNotEnoughAlices => _abortedNotEnoughAlicesMessage,
						_ => _roundFailedMessage
					};
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
}
