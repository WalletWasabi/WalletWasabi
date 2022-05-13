using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Windows.Input;
using Avalonia.Threading;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.State;
using WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Navigation;
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
	private readonly MusicStatusMessageViewModel _waitingForBlameRoundMessage = new() { Message = "Waiting for the blame round" };
	private readonly MusicStatusMessageViewModel _waitingRoundMessage = new() { Message = "Waiting for a round" };
	private readonly MusicStatusMessageViewModel _connectionConfirmationMessage = new() { Message = "Preparing coinjoin" };

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
		AutoFinished,

		Stopped,
		ManualPlaying,
		ManualFinished,
	}

	private enum Trigger
	{
		Invalid = 0,
		AutoStartTimeout,
		AutoCoinJoinOn,
		AutoCoinJoinOff,
		AutoCoinJoinEntered,
		ManualCoinJoinEntered,
		Pause,
		Play,
		Stop,
		PlebStop,
		RoundStartFailed,
		RoundStart,
		RoundFinished,
		BalanceChanged,
		Timer
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
			.Permit(Trigger.ManualCoinJoinEntered, State.Stopped)
			.OnEntry(() =>
			{
				IsAuto = false;
				IsAutoWaiting = false;
				PlayVisible = true;
				StopVisible = false;
				PauseVisible = false;

				_stateMachine.Fire(Trigger.ManualCoinJoinEntered);
			})
			.OnEntry(UpdateAndShowWalletMixedProgress)
			.OnTrigger(Trigger.BalanceChanged, UpdateAndShowWalletMixedProgress);

		_stateMachine.Configure(State.Stopped)
			.SubstateOf(State.ManualCoinJoin)
			.Permit(Trigger.Play, State.ManualPlaying)
			.OnEntry(async () =>
			{
				ProgressValue = 0;
				StopVisible = false;
				PlayVisible = true;
				_wallet.AllowManualCoinJoin = false;
				CurrentStatus = _stoppedMessage;
				await coinJoinManager.StopAsync(_wallet, CancellationToken.None);
			})
			.OnEntry(UpdateAndShowWalletMixedProgress)
			.OnTrigger(Trigger.BalanceChanged, UpdateAndShowWalletMixedProgress);

		_stateMachine.Configure(State.ManualPlaying)
			.SubstateOf(State.ManualCoinJoin)
			.Permit(Trigger.Stop, State.Stopped)
			.Permit(Trigger.RoundStartFailed, State.ManualFinished)
			.OnEntry(async () =>
			{
				PlayVisible = false;
				StopVisible = true;
				CurrentStatus = _waitingMessage;
				await coinJoinManager.StartAsync(_wallet, CancellationToken.None);
			})
			.OnEntry(UpdateAndShowWalletMixedProgress)
			.OnTrigger(Trigger.BalanceChanged, UpdateAndShowWalletMixedProgress)
			.OnTrigger(Trigger.RoundFinished, async () => await coinJoinManager.StartAsync(_wallet, CancellationToken.None))
			.OnTrigger(Trigger.Timer, UpdateCountDown);

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

		// AutoCj State
		_stateMachine.Configure(State.AutoCoinJoin)
			.Permit(Trigger.AutoCoinJoinOff, State.ManualCoinJoin)
			.Permit(Trigger.AutoCoinJoinEntered, State.AutoStarting)
			.Permit(Trigger.RoundStart, State.AutoPlaying)
			.Permit(Trigger.RoundStartFailed, State.AutoFinished)
			.OnEntry(async () =>
			{
				IsAuto = true;
				StopVisible = false;
				PauseVisible = false;
				PlayVisible = false;

				CurrentStatus = _initialisingMessage;

				await coinJoinManager.StopAsync(_wallet, CancellationToken.None);

				_stateMachine.Fire(Trigger.AutoCoinJoinEntered);
			});

		_stateMachine.Configure(State.AutoStarting)
			.SubstateOf(State.AutoCoinJoin)
			.Permit(Trigger.Pause, State.Paused)
			.Permit(Trigger.RoundStart, State.AutoPlaying)
			.Permit(Trigger.RoundStartFailed, State.AutoFinished)
			.Permit(Trigger.AutoStartTimeout, State.AutoPlaying)
			.Permit(Trigger.Play, State.AutoPlaying)
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

				PauseVisible = false;
				PlayVisible = true;

				await coinJoinManager.StopAsync(_wallet, CancellationToken.None);
			})
			.OnEntry(UpdateAndShowWalletMixedProgress)
			.OnTrigger(Trigger.BalanceChanged, UpdateAndShowWalletMixedProgress);

		_stateMachine.Configure(State.AutoPlaying)
			.Permit(Trigger.AutoCoinJoinOff, State.ManualCoinJoin)
			.Permit(Trigger.AutoCoinJoinEntered, State.AutoStarting)
			.Permit(Trigger.Pause, State.Paused)
			.Permit(Trigger.PlebStop, State.Paused)
			.Permit(Trigger.RoundStartFailed, State.AutoFinished)
			.OnEntry(async () =>
			{
				CurrentStatus = _waitingMessage;
				IsAutoWaiting = false;
				PauseVisible = true;
				PlayVisible = false;
				await coinJoinManager.StartAutomaticallyAsync(_wallet, CancellationToken.None);
			})
			.OnEntry(UpdateAndShowWalletMixedProgress)
			.OnTrigger(Trigger.BalanceChanged, UpdateAndShowWalletMixedProgress)
			.OnTrigger(Trigger.Timer, UpdateCountDown);

		_stateMachine.Configure(State.AutoFinished)
			.SubstateOf(State.AutoCoinJoin)
			.Permit(Trigger.RoundStart, State.AutoPlaying)
			.OnEntry(() =>
			{
				PauseVisible = false;

				ProgressValue = 100;
				ElapsedTime = "";
				RemainingTime = "";

				CurrentStatus = _finishedMessage;
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

		var privateThreshold = _wallet.KeyManager.MinAnonScoreTarget;

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
				CurrentStatus = roundEnded.RoundState.WasTransactionBroadcast ? _roundSucceedMessage : _roundFailedMessage;
				StopCountDown();
				break;

			case EnteringOutputRegistrationPhase enteringOutputRegistrationPhase:
				StartCountDown(
					message: _outputRegistrationMessage,
					start: enteringOutputRegistrationPhase.TimeoutAt - enteringOutputRegistrationPhase.RoundState.CoinjoinState.Parameters.OutputRegistrationTimeout,
					end: enteringOutputRegistrationPhase.TimeoutAt);
				break;

			case EnteringSigningPhase enteringSigningPhase:
				StartCountDown(
					message: _transactionSigningMessage,
					start: enteringSigningPhase.TimeoutAt - enteringSigningPhase.RoundState.CoinjoinState.Parameters.TransactionSigningTimeout,
					end: enteringSigningPhase.TimeoutAt);
				break;

			case EnteringInputRegistrationPhase enteringInputRegistrationPhase:
				StartCountDown(
					message: _inputRegistrationMessage,
					start: enteringInputRegistrationPhase.TimeoutAt - enteringInputRegistrationPhase.RoundState.CoinjoinState.Parameters.StandardInputRegistrationTimeout,
					end: enteringInputRegistrationPhase.TimeoutAt);
				break;

			case WaitingForBlameRound waitingForBlameRound:
				StartCountDown(message: _waitingForBlameRoundMessage, start: DateTimeOffset.UtcNow, end: waitingForBlameRound.TimeoutAt);
				break;

			case WaitingForRound:
				CurrentStatus = _waitingRoundMessage;
				StopCountDown();
				break;

			case EnteringConnectionConfirmationPhase enteringConnectionConfirmationPhase:
				StartCountDown(
					message: _connectionConfirmationMessage,
					start: enteringConnectionConfirmationPhase.TimeoutAt - enteringConnectionConfirmationPhase.RoundState.CoinjoinState.Parameters.ConnectionConfirmationTimeout,
					end: enteringConnectionConfirmationPhase.TimeoutAt);
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
