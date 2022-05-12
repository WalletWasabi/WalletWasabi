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
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.StatusChangedEvents;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public partial class CoinJoinStateViewModel : ViewModelBase
{
	private readonly StateMachine<State, Trigger> _stateMachine;
	private readonly Wallet _wallet;
	private readonly DispatcherTimer _countdownTimer;

	private readonly MusicStatusMessageViewModel _countDownMessage = new() { Message = "Waiting to auto-start coinjoin" };

	private readonly MusicStatusMessageViewModel _coinJoiningMessage = new() { Message = "Coinjoining" };

	private readonly MusicStatusMessageViewModel _pauseMessage = new() { Message = "Coinjoin is paused" };

	private readonly MusicStatusMessageViewModel _stoppedMessage = new() { Message = "Coinjoin is stopped" };

	private readonly MusicStatusMessageViewModel _initialisingMessage = new() { Message = "Coinjoin is initialising" };

	private readonly MusicStatusMessageViewModel _finishedMessage = new() { Message = "Not enough non-private funds to coinjoin" };

	private readonly MusicStatusMessageViewModel _plebStopMessage = new() { Message = "Below the threshold. Press play to override." };

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

	private DateTimeOffset _countDownStartTime;
	private DateTimeOffset _countDownEndTime;
	private bool _overridePlebStop;

	public CoinJoinStateViewModel(WalletViewModel walletVm, IObservable<Unit> balanceChanged)
	{
		_wallet = walletVm.Wallet;

		_elapsedTime = "";
		_remainingTime = "";

		var now = DateTimeOffset.UtcNow;
		_countDownStartTime = now;
		_countDownEndTime = now + TimeSpan.FromSeconds(Random.Shared.Next(5 * 60, 16 * 60));

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

		PlayCommand = ReactiveCommand.Create(() => _stateMachine.Fire(Trigger.Play));

		PauseCommand = ReactiveCommand.Create(() => _stateMachine.Fire(Trigger.Pause));

		StopCommand = ReactiveCommand.Create(() => _stateMachine.Fire(Trigger.Stop));

		walletVm.Settings.WhenAnyValue(x => x.AutoCoinJoin)
			.Subscribe(SetAutoCoinJoin);

		walletVm.Settings.WhenAnyValue(x => x.PlebStopThreshold)
			.Subscribe(x => _stateMachine.Fire(Trigger.PlebStopChanged));

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
		AutoFinishedPlebStop,

		Stopped,
		ManualPlaying,
		ManualFinished,

		ManualFinishedPlebStop,
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
		Timer,
		PlebStopChanged
	}

	private bool AutoStartTimedOut => GetRemainingTime() <= TimeSpan.Zero;

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
			.Permit(Trigger.ManualCoinJoinEntered, State.Stopped) // TODO remove hack
			.OnEntry(() =>
			{
				IsAuto = false;
				IsAutoWaiting = false;
				PlayVisible = true;
				StopVisible = false;
				PauseVisible = false;

				_overridePlebStop = false;

				_stateMachine.Fire(Trigger.ManualCoinJoinEntered);
			})
			.OnEntry(UpdateWalletMixedProgress)
			.OnTrigger(Trigger.BalanceChanged, UpdateWalletMixedProgress);

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
				_overridePlebStop = false;

				await coinJoinManager.StopAsync(_wallet, CancellationToken.None);
			})
			.OnEntry(UpdateWalletMixedProgress)
			.OnTrigger(Trigger.BalanceChanged, UpdateWalletMixedProgress);

		_stateMachine.Configure(State.ManualPlaying)
			.SubstateOf(State.ManualCoinJoin)
			.Permit(Trigger.Stop, State.Stopped)
			.Permit(Trigger.RoundStartFailed, State.ManualFinished)
			.Permit(Trigger.PlebStop, State.ManualFinishedPlebStop)
			.OnEntry(async () =>
			{
				PlayVisible = false;
				StopVisible = true;
				CurrentStatus = _coinJoiningMessage;

				if (_overridePlebStop && !_wallet.IsUnderPlebStop)
				{
					// If we are not below the threshold anymore, we turn off the override.
					_overridePlebStop = false;
				}

				await coinJoinManager.StartAsync(_wallet, _overridePlebStop, CancellationToken.None);
			})
			.OnEntry(UpdateWalletMixedProgress)
			.OnTrigger(Trigger.BalanceChanged, UpdateWalletMixedProgress)
			.OnTrigger(Trigger.RoundFinished, async () => await coinJoinManager.StartAsync(_wallet, false, CancellationToken.None));

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
				ProgressValue = 0;

				StopVisible = true;
				PauseVisible = false;
				PlayVisible = true;
			})
			.OnTrigger(Trigger.Play, () =>
			{
				_overridePlebStop = true;
			});

		// AutoCj State
		_stateMachine.Configure(State.AutoCoinJoin)
			.Permit(Trigger.AutoCoinJoinOff, State.ManualCoinJoin)
			.Permit(Trigger.AutoCoinJoinEntered, State.AutoStarting) // TODO remove hack
			.OnEntry(async () =>
			{
				IsAuto = true;
				StopVisible = false;
				PauseVisible = false;
				PlayVisible = false;

				CurrentStatus = _initialisingMessage;

				_overridePlebStop = false;

				await coinJoinManager.StopAsync(_wallet, CancellationToken.None);

				_stateMachine.Fire(Trigger.AutoCoinJoinEntered);
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
				_countdownTimer.Start();
				PlayVisible = true;
				IsAutoWaiting = true;
				CurrentStatus = _countDownMessage;
			})
			.OnTrigger(Trigger.Timer, UpdateCountDown)
			.OnExit(() =>
			{
				_countdownTimer.Stop();
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

				_overridePlebStop = false;

				await coinJoinManager.StopAsync(_wallet, CancellationToken.None);
			})
			.OnEntry(UpdateWalletMixedProgress)
			.OnTrigger(Trigger.BalanceChanged, UpdateWalletMixedProgress);

		_stateMachine.Configure(State.AutoPlaying)
			.SubstateOf(State.AutoCoinJoin)
			.Permit(Trigger.Pause, State.Paused)
			.Permit(Trigger.PlebStop, State.AutoFinishedPlebStop)
			.Permit(Trigger.RoundStartFailed, State.AutoFinished)
			.OnEntry(async () =>
			{
				CurrentStatus = _coinJoiningMessage;
				IsAutoWaiting = false;
				PauseVisible = true;
				PlayVisible = false;

				if (_overridePlebStop && !_wallet.IsUnderPlebStop)
				{
					// If we are not below the threshold anymore, we turn off the override.
					_overridePlebStop = false;
				}

				await coinJoinManager.StartAutomaticallyAsync(_wallet, _overridePlebStop, CancellationToken.None); =
			})
			.OnEntry(UpdateWalletMixedProgress)
			.OnTrigger(Trigger.BalanceChanged, UpdateWalletMixedProgress);

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

	private void UpdateCountDown()
	{
		var format = @"hh\:mm\:ss";
		ElapsedTime = $"{GetElapsedTime().ToString(format)}";
		RemainingTime = $"-{GetRemainingTime().ToString(format)}";
		ProgressValue = GetPercentage();

		if (AutoStartTimedOut)
		{
			_stateMachine.Fire(Trigger.AutoStartTimeout);
		}
	}

	private TimeSpan GetElapsedTime() => DateTimeOffset.Now - _countDownStartTime;

	private TimeSpan GetRemainingTime() => _countDownEndTime - DateTimeOffset.Now;

	private TimeSpan GetTotalTime() => _countDownEndTime - _countDownStartTime;

	private double GetPercentage() => GetElapsedTime().TotalSeconds / GetTotalTime().TotalSeconds * 100;

	private void OnTimerTick(object? sender, EventArgs e)
	{
		_stateMachine.Fire(Trigger.Timer);
	}

	private void UpdateWalletMixedProgress()
	{
		if (!_wallet.Coins.Any())
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

			case StartErrorEventArgs start when start.Error is CoinjoinError.NotEnoughUnprivateBalance:
				_stateMachine.Fire(Trigger.PlebStop);
				break;

			case StartErrorEventArgs:
				_stateMachine.Fire(Trigger.RoundStartFailed);
				break;

			case CoinJoinStatusEventArgs coinJoinStatusEventArgs when coinJoinStatusEventArgs.Wallet == _wallet:
				IsInCriticalPhase = coinJoinStatusEventArgs.CoinJoinProgressEventArgs.IsInCriticalPhase;
				break;
		}
	}

	private void SetAutoCoinJoin(bool enabled)
	{
		_stateMachine.Fire(enabled ? Trigger.AutoCoinJoinOn : Trigger.AutoCoinJoinOff);
	}
}
