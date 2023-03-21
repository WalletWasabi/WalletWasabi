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
	private const string CountDownMessage = "Waiting to auto-start coinjoin";
	private const string WaitingMessage = "Waiting for coinjoin";
	private const string PauseMessage = "Coinjoin is paused";
	private const string StoppedMessage = "Coinjoin is stopped";
	private const string RoundSucceedMessage = "Successful coinjoin! Continuing...";
	private const string RoundFinishedMessage = "Round finished, waiting for next round";
	private const string AbortedNotEnoughAlicesMessage = "Not enough participants, retrying...";
	private const string CoinJoinInProgress = "Coinjoin in progress";
	private const string InputRegistrationMessage = "Waiting for other participants";
	private const string WaitingForBlameRoundMessage = "Waiting for the blame round";
	private const string WaitingRoundMessage = "Waiting for a round";
	private const string PlebStopMessage = "Coinjoining might be uneconomical";
	private const string PlebStopMessageBelow = "Receive more funds or press play to bypass";
	private const string WaitingForConfirmedFundsMessage = "Waiting for confirmed funds";
	private const string UserInSendWorkflowMessage = "Waiting for closed send dialog";
	private const string AllPrivateMessage = "Hurray! Your funds are private";
	private const string GeneralErrorMessage = "Waiting for valid conditions";

	private readonly StateMachine<State, Trigger> _stateMachine;
	private readonly DispatcherTimer _countdownTimer;
	private readonly DispatcherTimer _autoCoinJoinStartTimer;

	[AutoNotify] private bool _isAutoWaiting;
	[AutoNotify] private bool _playVisible;
	[AutoNotify] private bool _pauseVisible;
	[AutoNotify] private bool _pauseSpreading;
	[AutoNotify] private bool _stopVisible;
	[AutoNotify] private string _currentStatus = "";
	[AutoNotify] private bool _isProgressReversed;
	[AutoNotify] private double _progressValue;
	[AutoNotify] private string _leftText = "";
	[AutoNotify] private string _rightText = "";
	[AutoNotify] private bool _isInCriticalPhase;
	[AutoNotify] private bool _isCountDownDelayHappening;
	[AutoNotify] private bool _areAllCoinsPrivate;

	private DateTimeOffset _countDownStartTime;
	private DateTimeOffset _countDownEndTime;

	public CoinJoinStateViewModel(WalletViewModel walletVm)
	{
		WalletVm = walletVm;
		var wallet = walletVm.Wallet;

		var coinJoinManager = Services.HostedServices.Get<CoinJoinManager>();

		Observable.FromEventPattern<StatusChangedEventArgs>(coinJoinManager, nameof(coinJoinManager.StatusChanged))
			.Where(x => x.EventArgs.Wallet == walletVm.Wallet)
			.Select(x => x.EventArgs)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Do(ProcessStatusChange)
			.Subscribe();

		WalletVm.UiTriggers.PrivacyProgressUpdateTrigger
			.Select(_ => WalletVm.Wallet.IsWalletPrivate())
			.BindTo(this, x => x.AreAllCoinsPrivate);

		var initialState = walletVm.CoinJoinSettings.AutoCoinJoin
			? State.WaitingForAutoStart
			: State.StoppedOrPaused;

		if (walletVm.Wallet.KeyManager.IsHardwareWallet || walletVm.Wallet.KeyManager.IsWatchOnly)
		{
			initialState = State.Disabled;
		}

		_stateMachine = new StateMachine<State, Trigger>(initialState);

		ConfigureStateMachine();

		walletVm.UiTriggers.TransactionsUpdateTrigger.Subscribe(_ => _stateMachine.Fire(Trigger.BalanceChanged));

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

		var stopPauseCommandCanExecute = this.WhenAnyValue(
			x => x.IsInCriticalPhase,
			x => x.PauseSpreading,
			(isInCriticalPhase, pauseSpreading) => !isInCriticalPhase && !pauseSpreading);

		StopPauseCommand = ReactiveCommand.CreateFromTask(
			async () => await coinJoinManager.StopAsync(wallet, CancellationToken.None),
			stopPauseCommandCanExecute);

		AutoCoinJoinObservable = walletVm.CoinJoinSettings.WhenAnyValue(x => x.AutoCoinJoin);

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

		walletVm.CoinJoinSettings.WhenAnyValue(x => x.PlebStopThreshold)
			.SubscribeAsync(async _ =>
			{
				// Hack: we take the value from KeyManager but it is saved later.
				await Task.Delay(1500);
				_stateMachine.Fire(Trigger.PlebStopChanged);
			});

		_autoCoinJoinStartTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(Random.Shared.Next(5, 16)) };
		_autoCoinJoinStartTimer.Tick += async (_, _) =>
		{
			await coinJoinManager.StartAsync(wallet, stopWhenAllMixed: false, false, CancellationToken.None);
			_autoCoinJoinStartTimer.Stop();
		};

		_countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
		_countdownTimer.Tick += (_, _) => _stateMachine.Fire(Trigger.Tick);

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
		StartError,
		BalanceChanged,
		Tick,
		PlebStopChanged,
		WalletStartedCoinJoin,
		WalletStoppedCoinJoin,
		AutoCoinJoinOff
	}

	public bool IsAutoCoinJoinEnabled => WalletVm.CoinJoinSettings.AutoCoinJoin;

	public IObservable<bool> AutoCoinJoinObservable { get; }

	private bool IsCountDownFinished => GetRemainingTime() <= TimeSpan.Zero;

	private bool IsCounting => _countdownTimer.IsEnabled;

	public ICommand PlayCommand { get; }

	public ICommand StopPauseCommand { get; }

	public WalletViewModel WalletVm { get; }

	private void ConfigureStateMachine()
	{
		_stateMachine.Configure(State.Disabled);

		_stateMachine.Configure(State.WaitingForAutoStart)
			.Permit(Trigger.WalletStartedCoinJoin, State.Playing)
			.Permit(Trigger.AutoCoinJoinOff, State.StoppedOrPaused)
			.Permit(Trigger.PlebStopActivated, State.PlebStopActive)
			.Permit(Trigger.StartError, State.Playing)
			.OnEntry(() =>
			{
				PlayVisible = true;
				PauseVisible = false;
				StopVisible = false;
				IsAutoWaiting = true;

				var now = DateTimeOffset.UtcNow;
				var autoStartEnd = now + _autoCoinJoinStartTimer.Interval;
				_autoCoinJoinStartTimer.Start();

				StartCountDown(CountDownMessage, now, autoStartEnd);
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
				StopCountDown();
				PlayVisible = true;
				PauseVisible = false;
				PauseSpreading = false;
				StopVisible = false;
				CurrentStatus = IsAutoCoinJoinEnabled ? PauseMessage : StoppedMessage;
				LeftText = "Press Play to start";
			})
			.OnExit(() => LeftText = "");

		_stateMachine.Configure(State.Playing)
			.Permit(Trigger.WalletStoppedCoinJoin, State.StoppedOrPaused)
			.Permit(Trigger.PlebStopActivated, State.PlebStopActive)
			.OnEntry(() =>
			{
				PlayVisible = false;
				PauseVisible = IsAutoCoinJoinEnabled;
				StopVisible = !IsAutoCoinJoinEnabled;

				CurrentStatus = WaitingMessage;
			})
			.OnTrigger(Trigger.Tick, UpdateCountDown);

		_stateMachine.Configure(State.PlebStopActive)
			.Permit(Trigger.BalanceChanged, State.Playing)
			.Permit(Trigger.PlebStopChanged, State.Playing)
			.Permit(Trigger.WalletStartedCoinJoin, State.Playing)
			.Permit(Trigger.WalletStoppedCoinJoin, State.StoppedOrPaused)
			.Permit(Trigger.StartError, State.Playing)
			.OnEntry(() =>
			{
				PlayVisible = true;
				PauseVisible = false;
				StopVisible = false;

				CurrentStatus = PlebStopMessage;
				LeftText = PlebStopMessageBelow;
			})
			.OnExit(() => LeftText = "");
	}

	private void UpdateCountDown()
	{
		IsCountDownDelayHappening = IsCounting && IsCountDownFinished;

		// This case mostly happens when there is some delay between the client and the server,
		// and the countdown has finished but the client hasn't received any new phase changed message.
		if (IsCountDownDelayHappening)
		{
			LeftText = "Waiting for response";
			RightText = "";
			return;
		}

		var format = @"hh\:mm\:ss";
		LeftText = $"{GetElapsedTime().ToString(format)}";
		RightText = $"-{GetRemainingTime().ToString(format)}";
		ProgressValue = GetPercentage();
	}

	private TimeSpan GetElapsedTime() => DateTimeOffset.UtcNow - _countDownStartTime;

	private TimeSpan GetRemainingTime() => _countDownEndTime - DateTimeOffset.UtcNow;

	private TimeSpan GetTotalTime() => _countDownEndTime - _countDownStartTime;

	private double GetPercentage() => GetElapsedTime().TotalSeconds / GetTotalTime().TotalSeconds * 100;

	private void ProcessStatusChange(StatusChangedEventArgs e)
	{
		switch (e)
		{
			case WalletStartedCoinJoinEventArgs:
				_stateMachine.Fire(Trigger.WalletStartedCoinJoin);
				break;

			case WalletStoppedCoinJoinEventArgs:
				_stateMachine.Fire(Trigger.WalletStoppedCoinJoin);
				break;

			case StartErrorEventArgs start:
				if (start.Error is CoinjoinError.NotEnoughUnprivateBalance)
				{
					_stateMachine.Fire(Trigger.PlebStopActivated);
					break;
				}

				_stateMachine.Fire(Trigger.StartError);
				CurrentStatus = start.Error switch
				{
					CoinjoinError.NoCoinsToMix => WaitingForConfirmedFundsMessage,
					CoinjoinError.UserInSendWorkflow => UserInSendWorkflowMessage,
					CoinjoinError.AllCoinsPrivate => AllPrivateMessage,
					_ => GeneralErrorMessage
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
				if (roundEnded.IsStopped)
				{
					PauseSpreading = true;
				}
				else
				{
					CurrentStatus = roundEnded.LastRoundState.EndRoundState switch
					{
						EndRoundState.TransactionBroadcasted => RoundSucceedMessage,
						EndRoundState.AbortedNotEnoughAlices => AbortedNotEnoughAlicesMessage,
						_ => RoundFinishedMessage
					};
					StopCountDown();
				}
				break;

			case EnteringOutputRegistrationPhase outputRegPhase:
				_countDownEndTime = outputRegPhase.TimeoutAt + outputRegPhase.RoundState.CoinjoinState.Parameters.TransactionSigningTimeout;
				break;

			case EnteringSigningPhase signingPhase:
				_countDownEndTime = signingPhase.TimeoutAt;
				break;

			case EnteringInputRegistrationPhase inputRegPhase:
				StartCountDown(
					message: InputRegistrationMessage,
					start: inputRegPhase.TimeoutAt - inputRegPhase.RoundState.InputRegistrationTimeout,
					end: inputRegPhase.TimeoutAt);
				break;

			case WaitingForBlameRound waitingForBlameRound:
				StartCountDown(message: WaitingForBlameRoundMessage, start: DateTimeOffset.UtcNow, end: waitingForBlameRound.TimeoutAt);
				break;

			case WaitingForRound:
				CurrentStatus = WaitingRoundMessage;
				StopCountDown();
				break;

			case EnteringConnectionConfirmationPhase confirmationPhase:

				var startTime = confirmationPhase.TimeoutAt - confirmationPhase.RoundState.CoinjoinState.Parameters.ConnectionConfirmationTimeout;
				var totalEndTime = confirmationPhase.TimeoutAt +
								   confirmationPhase.RoundState.CoinjoinState.Parameters.OutputRegistrationTimeout +
								   confirmationPhase.RoundState.CoinjoinState.Parameters.TransactionSigningTimeout;

				StartCountDown(
					message: CoinJoinInProgress,
					start: startTime,
					end: totalEndTime);

				break;

			case EnteringCriticalPhase:
				IsInCriticalPhase = true;
				break;

			case LeavingCriticalPhase:
				IsInCriticalPhase = false;
				break;
		}
	}

	private void StartCountDown(string message, DateTimeOffset start, DateTimeOffset end)
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
		LeftText = "";
		RightText = "";
		ProgressValue = 0;
	}
}
