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
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public partial class CoinJoinStateViewModel : ViewModelBase
{
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

	private readonly StateMachine<State, Trigger> _stateMachine;
	private readonly Wallet _wallet;

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

	private readonly MusicStatusMessageViewModel _countDownMessage = new() { Message = "Waiting to auto-start coinjoin" };

	private readonly MusicStatusMessageViewModel _coinJoiningMessage = new() { Message = "Coinjoining" };

	private readonly MusicStatusMessageViewModel _pauseMessage = new() { Message = "Coinjoin is paused" };

	private readonly MusicStatusMessageViewModel _stoppedMessage = new() { Message = "Coinjoin is stopped" };

	private readonly MusicStatusMessageViewModel _initialisingMessage = new() { Message = "Coinjoin is initialising" };

	private readonly MusicStatusMessageViewModel _finishedMessage = new() { Message = "No balance to coinjoin" };

	private TimeSpan _autoStartTime;
	private DateTimeOffset _countDownStarted;

	public CoinJoinStateViewModel(WalletViewModel walletVm, IObservable<Unit> balanceChanged)
	{
		_elapsedTime = "";
		_remainingTime = "";
		_countDownStarted = DateTimeOffset.Now;
		_autoStartTime = TimeSpan.FromSeconds(Random.Shared.Next(5 * 60, 16 * 60));

		_wallet = walletVm.Wallet;

		DispatcherTimer.Run(() =>
		{
			TimerOnTick();
			return true;
		}, TimeSpan.FromSeconds(1));

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

		_stateMachine.Start();
	}

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
				await coinJoinManager.StopAsync(_wallet, CancellationToken.None);
			})
			.OnEntry(UpdateWalletMixedProgress)
			.OnTrigger(Trigger.BalanceChanged, UpdateWalletMixedProgress);

		_stateMachine.Configure(State.ManualPlaying)
			.SubstateOf(State.ManualCoinJoin)
			.Permit(Trigger.Stop, State.Stopped)
			.Permit(Trigger.RoundStartFailed, State.ManualFinished)
			.OnEntry(async () =>
			{
				PlayVisible = false;
				StopVisible = true;
				CurrentStatus = _coinJoiningMessage;
				await coinJoinManager.StartAsync(_wallet, CancellationToken.None);
			})
			.OnEntry(UpdateWalletMixedProgress)
			.OnTrigger(Trigger.BalanceChanged, UpdateWalletMixedProgress)
			.OnTrigger(Trigger.RoundFinished, async () =>
			{
				await coinJoinManager.StartAsync(_wallet, CancellationToken.None);
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

				if (AutoStartTimedOut)
				{
					_stateMachine.Fire(Trigger.AutoStartTimeout);
				}
				else
				{
					CurrentStatus = _countDownMessage;
					UpdateCountDown();
				}
			})
			.OnTrigger(Trigger.Timer, UpdateCountDown)
			.OnExit(() =>
			{
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
			.OnEntry(UpdateWalletMixedProgress)
			.OnTrigger(Trigger.BalanceChanged, UpdateWalletMixedProgress);

		_stateMachine.Configure(State.AutoPlaying)
			.Permit(Trigger.AutoCoinJoinOff, State.ManualCoinJoin)
			.Permit(Trigger.AutoCoinJoinEntered, State.AutoStarting)
			.Permit(Trigger.Pause, State.Paused)
			.Permit(Trigger.PlebStop, State.Paused)
			.Permit(Trigger.RoundStartFailed, State.AutoFinished)
			.OnEntry(async () =>
			{
				CurrentStatus = _coinJoiningMessage;
				IsAutoWaiting = false;
				PauseVisible = true;
				PlayVisible = false;
				_autoStartTime = TimeSpan.Zero;
				_countDownStarted = DateTimeOffset.Now;
				await coinJoinManager.StartAutomaticallyAsync(_wallet, CancellationToken.None);
			})
			.OnEntry(UpdateWalletMixedProgress)
			.OnTrigger(Trigger.BalanceChanged, UpdateWalletMixedProgress);

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

	private bool AutoStartTimedOut => GetRemainingTime() <= TimeSpan.Zero;

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

	private TimeSpan GetElapsedTime() => DateTimeOffset.Now - _countDownStarted;

	private TimeSpan GetRemainingTime() => (_countDownStarted + _autoStartTime) - DateTimeOffset.Now;

	private TimeSpan GetTotalTime() => _autoStartTime;

	private double GetPercentage() => GetElapsedTime().TotalSeconds / GetTotalTime().TotalSeconds * 100;

	private void TimerOnTick()
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

			case StartErrorEventArgs:
				_stateMachine.Fire(Trigger.RoundStartFailed);
				break;
		}
	}

	private void SetAutoCoinJoin(bool enabled)
	{
		_stateMachine.Fire(enabled ? Trigger.AutoCoinJoinOn : Trigger.AutoCoinJoinOff);
	}

	public ICommand PlayCommand { get; }

	public ICommand PauseCommand { get; }

	public ICommand StopCommand { get; }
}
