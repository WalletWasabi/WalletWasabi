using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
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
	enum State
	{
		Disabled,
		AutoCoinJoin,
		ManualCoinJoin,

		AutoStarting,
		Paused,
		AutoPlaying,

		Stopped,
		ManualPlaying,
	}

	enum Trigger
	{
		AutoCoinJoinOn,
		AutoCoinJoinOff,
		AutoCoinJoinEntered,
		ManualCoinJoinEntered,
		Pause,
		Play,
		Stop,
		PlebStop,
		RoundStartFailed,
		RoundStart
	}

	private readonly StateMachine<State, Trigger> _machine;
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

	private readonly MusicStatusMessageViewModel _countDownMessage = new()
		{ Message = "Waiting to auto-start coinjoin" };

	private readonly MusicStatusMessageViewModel _coinJoiningMessage = new() { Message = "Coinjoining" };

	private readonly MusicStatusMessageViewModel _pauseMessage = new()
		{ Message = "Coinjoin is paused" };

	private readonly MusicStatusMessageViewModel _stoppedMessage = new() { Message = "Coinjoin is stopped" };

	private DateTimeOffset _autoStartTime;
	private DateTimeOffset _countDownStarted;

	public CoinJoinStateViewModel(WalletViewModel walletVm, IObservable<Unit> balanceChanged)
	{
		_elapsedTime = "";
		_remainingTime = "";

		_wallet = walletVm.Wallet;

		var timer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(1),
		};

		var coinJoinManager = Services.HostedServices.Get<CoinJoinManager>();

		Observable.FromEventPattern<StatusChangedEventArgs>(coinJoinManager, nameof(coinJoinManager.StatusChanged))
			.Where(x => x.EventArgs.Wallet == walletVm.Wallet)
			.Select(x=>x.EventArgs)
			.Subscribe(StatusChanged);

		var initialState = walletVm.Settings.AutoCoinJoin
			? State.AutoCoinJoin
			: State.ManualCoinJoin;

		if (walletVm.Wallet.KeyManager.IsHardwareWallet || walletVm.Wallet.KeyManager.IsWatchOnly)
		{
			initialState = State.Disabled;
		}

		_machine =
			new StateMachine<State, Trigger>(initialState);

		// See diagram in the developer docs.
		_machine.Configure(State.Disabled);

		// Manual Cj State
		_machine.Configure(State.ManualCoinJoin)
			.Permit(Trigger.AutoCoinJoinOn, State.AutoCoinJoin)
			.Permit(Trigger.ManualCoinJoinEntered, State.Stopped)
			.OnEntry(() =>
			{
				IsAuto = false;
				IsAutoWaiting = false;
				PlayVisible = true;
				StopVisible = false;
				PauseVisible = false;

				_machine.Fire(Trigger.ManualCoinJoinEntered);
			})
			.OnProcess(UpdateWalletMixedProgress);

		_machine.Configure(State.Stopped)
			.SubstateOf(State.ManualCoinJoin)
			.Permit(Trigger.Play, State.ManualPlaying)
			.OnEntry(() =>
			{
				ProgressValue = 0;
				StopVisible = false;
				PlayVisible = true;
				walletVm.Wallet.AllowManualCoinJoin = false;
				CurrentStatus = _stoppedMessage;
				coinJoinManager.Stop(walletVm.Wallet);
			})
			.OnProcess(UpdateWalletMixedProgress);

		_machine.Configure(State.ManualPlaying)
			.SubstateOf(State.ManualCoinJoin)
			.Permit(Trigger.Stop, State.Stopped)
			.OnEntry(() =>
			{
				PlayVisible = false;
				StopVisible = true;
				CurrentStatus = _coinJoiningMessage;
				coinJoinManager.Start(walletVm.Wallet);
			})
			.OnProcess(UpdateWalletMixedProgress);

		// AutoCj State
		_machine.Configure(State.AutoCoinJoin)
			.Permit(Trigger.AutoCoinJoinOff, State.ManualCoinJoin)
			.Permit(Trigger.AutoCoinJoinEntered, State.AutoStarting)
			.OnEntry(() =>
			{
				IsAuto = true;
				StopVisible = false;
				PauseVisible = false;
				PlayVisible = true;

				coinJoinManager.AutoStart(walletVm.Wallet);

				_machine.Fire(Trigger.AutoCoinJoinEntered);
			});

		_machine.Configure(State.AutoStarting)
			.SubstateOf(State.AutoCoinJoin)
			.Permit(Trigger.Pause, State.Paused)
			.Permit(Trigger.RoundStart, State.AutoPlaying)
			.Permit(Trigger.Play, State.AutoPlaying)
			.OnEntry(() =>
			{
				IsAutoWaiting = true;
				CurrentStatus = _countDownMessage;
				timer.Start();
			})
			.OnProcess(() =>
			{
				ElapsedTime = $"{DateTime.Now - _countDownStarted:mm\\:ss}";
				RemainingTime = $"-{_autoStartTime - DateTime.Now:mm\\:ss}";

				var total = (_autoStartTime - _countDownStarted).TotalSeconds;
				var percentage = (DateTime.Now - _countDownStarted).TotalSeconds * 100 / total;
				ProgressValue = percentage;
			})
			.OnExit(() =>
			{
				IsAutoWaiting = false;
				timer.Stop();
			});

		_machine.Configure(State.Paused)
			.SubstateOf(State.AutoCoinJoin)
			.Permit(Trigger.Play, State.AutoPlaying)
			.OnEntry(() =>
			{
				IsAutoWaiting = true;

				CurrentStatus = _pauseMessage;
				ProgressValue = 0;

				PauseVisible = false;
				PlayVisible = true;

				coinJoinManager.Stop(walletVm.Wallet);
			})
			.OnProcess(UpdateWalletMixedProgress);

		_machine.Configure(State.AutoPlaying)
			.SubstateOf(State.AutoCoinJoin)
			.Permit(Trigger.Pause, State.Paused)
			.Permit(Trigger.PlebStop, State.Paused)
			.Permit(Trigger.RoundStartFailed, State.Paused)
			.Permit(Trigger.RoundStart, State.AutoPlaying)
			.OnEntry(() =>
			{
				IsAutoWaiting = false;
				PauseVisible = true;
				PlayVisible = false;
				CurrentStatus = _coinJoiningMessage;
				coinJoinManager.Start(walletVm.Wallet);
			})
			.OnProcess(UpdateWalletMixedProgress);

		balanceChanged.Subscribe(_ => _machine.Process());

		PlayCommand = ReactiveCommand.Create(() => _machine.Fire(Trigger.Play));

		PauseCommand = ReactiveCommand.Create(() => _machine.Fire(Trigger.Pause));

		StopCommand = ReactiveCommand.Create(() => _machine.Fire(Trigger.Stop));

		timer.Tick += TimerOnTick;

		walletVm.Settings.WhenAnyValue(x => x.AutoCoinJoin)
			.Subscribe(SetAutoCoinJoin);

		_machine.Start();
	}

	private void TimerOnTick(object? sender, EventArgs e)
	{
		_machine.Process();
	}

	private void UpdateWalletMixedProgress()
	{
		if (_wallet.Coins.Any())
		{
			var privateThreshold = _wallet.KeyManager.MinAnonScoreTarget;

			var privateAmount = _wallet.Coins.FilterBy(x => x.HdPubKey.AnonymitySet >= privateThreshold).TotalAmount();
			var normalAmount = _wallet.Coins.FilterBy(x => x.HdPubKey.AnonymitySet < privateThreshold).TotalAmount();
			var total = _wallet.Coins.TotalAmount();

			ElapsedTime = "Balance to coinjoin:";
			RemainingTime = normalAmount.ToFormattedString() + "BTC";

			var percentage = privateAmount.ToDecimal(MoneyUnit.BTC) / total.ToDecimal(MoneyUnit.BTC) * 100;

			ProgressValue = (double)percentage;
		}
	}

	private void StatusChanged(StatusChangedEventArgs e)
	{
		switch (e)
		{
			case StartingEventArgs startingEventArgs:
				_countDownStarted = DateTimeOffset.Now;
				_autoStartTime = _countDownStarted + startingEventArgs.StartingIn;
				break;

			case StartedEventArgs:
				_machine.Fire(Trigger.RoundStart);
				break;

			case StartErrorEventArgs startErrorEventArgs:
				Console.WriteLine(startErrorEventArgs.Error);
				_machine.Fire(Trigger.RoundStartFailed);
				break;
		}
	}

	public void SetAutoCoinJoin(bool enabled)
	{
		_machine.Fire(enabled ? Trigger.AutoCoinJoinOn : Trigger.AutoCoinJoinOff);
	}

	public ICommand PlayCommand { get; }

	public ICommand PauseCommand { get; }

	public ICommand StopCommand { get; }
}