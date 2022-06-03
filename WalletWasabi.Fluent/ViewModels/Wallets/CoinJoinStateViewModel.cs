using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.State;
using WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.CoinJoinProgressEvents;
using WalletWasabi.WabiSabi.Client.StatusChangedEvents;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public partial class CoinJoinStateViewModel : ViewModelBase
{
	private readonly StateMachine<State, Trigger> _stateMachine;
	private readonly DispatcherTimer _countdownTimer;
	private readonly DispatcherTimer _autoCoinJoinStartTimer;

	private readonly MusicStatusMessageViewModel _countDownMessage = new() { Message = "Waiting to auto-start coinjoin" };
	private readonly MusicStatusMessageViewModel _waitingMessage = new() { Message = "Waiting for coinjoin" };
	private readonly MusicStatusMessageViewModel _pauseMessage = new() { Message = "Coinjoin is paused" };
	private readonly MusicStatusMessageViewModel _stoppedMessage = new() { Message = "Coinjoin is stopped" };
	private readonly MusicStatusMessageViewModel _roundSucceedMessage = new() { Message = "Successful coinjoin, continuing..." };
	private readonly MusicStatusMessageViewModel _roundFinishedMessage = new() { Message = "Round finished, waiting for the next round" };
	private readonly MusicStatusMessageViewModel _abortedNotEnoughAlicesMessage = new() { Message = "Not enough participants, retrying..." };
	private readonly MusicStatusMessageViewModel _outputRegistrationMessage = new() { Message = "Constructing coinjoin" };
	private readonly MusicStatusMessageViewModel _inputRegistrationMessage = new() { Message = "Waiting for other participants" };
	private readonly MusicStatusMessageViewModel _transactionSigningMessage = new() { Message = "Finalizing coinjoin" };
	private readonly MusicStatusMessageViewModel _waitingForBlameRoundMessage = new() { Message = "Waiting for the fallback round" };
	private readonly MusicStatusMessageViewModel _waitingRoundMessage = new() { Message = "Waiting for a round" };
	private readonly MusicStatusMessageViewModel _connectionConfirmationMessage = new() { Message = "Preparing coinjoin" };
	private readonly MusicStatusMessageViewModel _plebStopMessage = new() { Message = "Coinjoining might be uneconomical" };
	private readonly MusicStatusMessageViewModel _plebStopMessageBelow = new() { Message = "Receive more funds or press play to bypass" };

	[AutoNotify] private bool _isAutoWaiting;
	[AutoNotify] private bool _playVisible = true;
	[AutoNotify] private bool _pauseVisible;
	[AutoNotify] private bool _stopVisible;
	[AutoNotify] private MusicStatusMessageViewModel? _currentStatus;
	[AutoNotify] private bool _isProgressReversed;
	[AutoNotify] private double _progressValue;
	[AutoNotify] private string _elapsedTime;
	[AutoNotify] private string _remainingTime;
	[AutoNotify] private bool _isInCriticalPhase;
	[AutoNotify] private bool _isCountDownDelayHappening;

	private DateTimeOffset _countDownStartTime;
	private DateTimeOffset _countDownEndTime;

	public bool IsAutoCoinJoinEnabled => WalletVm.Settings.AutoCoinJoin;

	public CoinJoinStateViewModel(WalletViewModel walletVm, IObservable<Unit> balanceChanged)
	{
		WalletVm = walletVm;
		var wallet = walletVm.Wallet;

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

		var initialState = walletVm.Settings.AutoCoinJoin
			? State.WaitingForAutoStart
			: State.StoppedOrPaused;

		if (walletVm.Wallet.KeyManager.IsHardwareWallet || walletVm.Wallet.KeyManager.IsWatchOnly)
		{
			initialState = State.Disabled;
		}

		_stateMachine = new StateMachine<State, Trigger>(initialState);

		ConfigureStateMachine();

		balanceChanged.Subscribe(_ => _stateMachine.Fire(Trigger.BalanceChanged));

		PlayCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			if (!wallet.KeyManager.IsCoinjoinProfileSelected)
			{
				await RoutableViewModel.NavigateDialogAsync(new CoinJoinProfilesViewModel(wallet.KeyManager, isNewWallet: false), NavigationTarget.DialogScreen);
			}

			if (wallet.KeyManager.IsCoinjoinProfileSelected)
			{
				var overridePlebStop = _stateMachine.IsInState(State.PlebStopActive);
				await coinJoinManager.StartAsync(wallet, stopWhenAllMixed: !IsAutoCoinJoinEnabled, overridePlebStop, CancellationToken.None);
			}
		});

		StopPauseCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			await coinJoinManager.StopAsync(wallet, CancellationToken.None);
		});

		AutoCoinJoinObservable = walletVm.Settings.WhenAnyValue(x => x.AutoCoinJoin);

		AutoCoinJoinObservable
			.Skip(1) // The first one is triggered at the creation.
			.SubscribeAsync(async (autoCoinJoin) =>
			{
				if (autoCoinJoin)
				{
					await coinJoinManager.StartAsync(wallet, stopWhenAllMixed: false, false, CancellationToken.None);
				}
				else
				{
					await coinJoinManager.StopAsync(wallet, CancellationToken.None);
					_stateMachine.Fire(Trigger.AutoCoinJoinOff);
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
			await coinJoinManager.StartAsync(wallet, stopWhenAllMixed: false, false, CancellationToken.None);
			_autoCoinJoinStartTimer.Stop();
		};

		_stateMachine.Start();
	}

	private enum State
	{
		Invalid = 0,
		Disabled,
		StoppedOrPaused,
		Playing,
		PlebStopActive,
		WaitingForAutoStart,
	}

	private enum Trigger
	{
		Invalid = 0,
		PlebStopActivated,
		BalanceChanged,
		Tick,
		PlebStopChanged,
		WalletStartedCoinJoin,
		WalletStoppedCoinJoin,
		AutoCoinJoinOff
	}

	public IObservable<bool> AutoCoinJoinObservable { get; }

	private bool IsCountDownFinished => GetRemainingTime() <= TimeSpan.Zero;

	private bool IsCounting => _countdownTimer.IsEnabled;

	public ICommand PlayCommand { get; }

	public ICommand StopPauseCommand { get; }

	public WalletViewModel WalletVm { get; }

	private void ConfigureStateMachine()
	{
		// See diagram in the developer docs.
		_stateMachine.Configure(State.Disabled);

		_stateMachine.Configure(State.WaitingForAutoStart)
			.Permit(Trigger.WalletStartedCoinJoin, State.Playing)
			.Permit(Trigger.AutoCoinJoinOff, State.StoppedOrPaused)
			.OnEntry(() =>
			{
				PlayVisible = true;
				PauseVisible = false;
				StopVisible = false;
				IsAutoWaiting = true;

				var now = DateTimeOffset.UtcNow;
				var autoStartEnd = now + _autoCoinJoinStartTimer.Interval;
				_autoCoinJoinStartTimer.Start();

				StartCountDown(_countDownMessage, now, autoStartEnd);
			})
			.OnExit(() =>
			{
				IsAutoWaiting = false;
				_autoCoinJoinStartTimer.Stop();
				StopCountDown();
			})
			.OnTrigger(Trigger.Tick, UpdateCountDown);

		_stateMachine.Configure(State.StoppedOrPaused)
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
			.Permit(Trigger.WalletStoppedCoinJoin, State.StoppedOrPaused)
			.Permit(Trigger.PlebStopActivated, State.PlebStopActive)
			.OnEntry(() =>
			{
				PlayVisible = false;
				PauseVisible = IsAutoCoinJoinEnabled;
				StopVisible = !IsAutoCoinJoinEnabled;

				CurrentStatus = _waitingMessage;
			})
			.OnTrigger(Trigger.Tick, UpdateCountDown);

		_stateMachine.Configure(State.PlebStopActive)
			.Permit(Trigger.BalanceChanged, State.Playing)
			.Permit(Trigger.PlebStopChanged, State.Playing)
			.Permit(Trigger.WalletStartedCoinJoin, State.Playing)
			.Permit(Trigger.WalletStoppedCoinJoin, State.StoppedOrPaused)
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

			case StartErrorEventArgs start when start.Error is CoinjoinError.NotEnoughUnprivateBalance:
				_stateMachine.Fire(Trigger.PlebStopActivated);
				break;

			case StartErrorEventArgs start:
				CurrentStatus = start.Error switch
				{
					CoinjoinError.NoCoinsToMix => new() { Message = "Waiting for confirmed funds" },
					CoinjoinError.UserInSendWorkflow => new() { Message = "Waiting for closed send dialog" },
					CoinjoinError.AllCoinsPrivate => new() { Message = "Hurray!! Your wallet is private" },
					_ => new() { Message = "Waiting for valid conditions" },
				};
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
				CurrentStatus = roundEnded.LastRoundState.EndRoundState switch
				{
					EndRoundState.TransactionBroadcasted => _roundSucceedMessage,
					EndRoundState.AbortedNotEnoughAlices => _abortedNotEnoughAlicesMessage,
					_ => _roundFinishedMessage
				};
				StopCountDown();
				break;

			case EnteringOutputRegistrationPhase outputRegPhase:
				StartCountDown(
					message: _outputRegistrationMessage,
					start: outputRegPhase.TimeoutAt - outputRegPhase.RoundState.CoinjoinState.Parameters.OutputRegistrationTimeout,
					end: outputRegPhase.TimeoutAt);
				break;

			case EnteringSigningPhase signingPhase:
				StartCountDown(
					message: _transactionSigningMessage,
					start: signingPhase.TimeoutAt - signingPhase.RoundState.CoinjoinState.Parameters.TransactionSigningTimeout,
					end: signingPhase.TimeoutAt);
				break;

			case EnteringInputRegistrationPhase inputRegPhase:
				StartCountDown(
					message: _inputRegistrationMessage,
					start: inputRegPhase.TimeoutAt - inputRegPhase.RoundState.InputRegistrationTimeout,
					end: inputRegPhase.TimeoutAt);
				break;

			case WaitingForBlameRound waitingForBlameRound:
				StartCountDown(message: _waitingForBlameRoundMessage, start: DateTimeOffset.UtcNow, end: waitingForBlameRound.TimeoutAt);
				break;

			case WaitingForRound:
				CurrentStatus = _waitingRoundMessage;
				StopCountDown();
				break;

			case EnteringConnectionConfirmationPhase confirmationPhase:
				StartCountDown(
					message: _connectionConfirmationMessage,
					start: confirmationPhase.TimeoutAt - confirmationPhase.RoundState.CoinjoinState.Parameters.ConnectionConfirmationTimeout,
					end: confirmationPhase.TimeoutAt);
				break;

			case EnteringCriticalPhase:
				IsInCriticalPhase = true;
				break;

			case LeavingCriticalPhase:
				IsInCriticalPhase = false;
				break;
		}
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
		ElapsedTime = "";
		RemainingTime = "";
		ProgressValue = 0;
	}

	private void OnTimerTick(object? sender, EventArgs e)
	{
		_stateMachine.Fire(Trigger.Tick);
	}
}
