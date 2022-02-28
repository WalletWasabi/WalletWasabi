using System.Windows.Input;
using Avalonia.Threading;
using ReactiveUI;
using WalletWasabi.Fluent.State;
using WalletWasabi.WabiSabi.Client;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public enum State
{
	AutoCoinJoin,
	ManualCoinJoin,

	AutoStarting,
	Paused,
	AutoPlaying,
	AutoLoading,
	AutoFinished,

	Stopped,
	ManualPlaying,
	ManualLoading,
	ManualFinished
}

public enum Trigger
{
	AutoCoinJoinOn,
	AutoCoinJoinOff,
	AutoCoinJoinEntered,
	ManualCoinJoinEntered,
	Pause,
	Play,
	Stop,
	AutoPlayTimeout,
	PlebStop,
	CoinReceive,
	RoundComplete,
	RoundStart
}

public partial class CoinJoinStateViewModel : ViewModelBase
{
	private readonly StateMachine<State, Trigger> _machine;

	[AutoNotify] private bool _isAutoWaiting;
	[AutoNotify] private bool _isAuto;
	[AutoNotify] private bool _playVisible = true;
	[AutoNotify] private bool _pauseVisible;
	[AutoNotify] private bool _stopVisible;
	[AutoNotify] private MusicStatusMessageViewModel? _currentStatus;
	[AutoNotify] private bool _isProgressReversed;
	[AutoNotify] private double _progressValue;

	private readonly AutoUpdateMusicStatusMessageViewModel _countDownMessage;
	private readonly MusicStatusMessageViewModel _coinJoiningMessage = new() { Message = "Coinjoining" };

	private readonly MusicStatusMessageViewModel _pauseMessage = new() { Message = "Coinjoin is paused" };
	private readonly MusicStatusMessageViewModel _stoppedMessage = new() { Message = "Coinjoin is stopped" };
	private DateTime _autoStartTime;
	private DateTime _countDownStarted;

	public CoinJoinStateViewModel(WalletViewModel walletVm)
	{
		var coinJoinManager = Services.HostedServices.Get<CoinJoinManager>();

		coinJoinManager.StatusChanged += StatusChanged;

		_countDownMessage = new(() =>
			$"CoinJoin will auto-start in: {DateTime.Now - _autoStartTime:mm\\:ss}");

		_machine =
			new StateMachine<State, Trigger>(walletVm.Settings.AutoCoinJoin
				? State.AutoCoinJoin
				: State.ManualCoinJoin);


		// See diagram in the developer docs.
		// Manual Cj State
		_machine.Configure(State.ManualCoinJoin)
			.Permit(Trigger.AutoCoinJoinOn, State.AutoCoinJoin)
			.Permit(Trigger.ManualCoinJoinEntered, State.Stopped)
			.OnEntry(OnEnterManualCoinJoin);

		_machine.Configure(State.Stopped)
			.SubstateOf(State.ManualCoinJoin)
			.OnEntry(()=>
			{
				ProgressValue = 0;
				StopVisible = false;
				PlayVisible = true;
				walletVm.Wallet.AllowManualCoinJoin = false;
				CurrentStatus = _stoppedMessage;
				coinJoinManager.Stop(walletVm.Wallet);
			})
			.Permit(Trigger.Play, State.ManualPlaying);

		_machine.Configure(State.ManualPlaying)
			.Permit(Trigger.Stop, State.Stopped)
			.Permit(Trigger.RoundComplete, State.ManualFinished)
			.Permit(Trigger.RoundComplete, State.ManualLoading)
			.OnEntry(() =>
			{
				PlayVisible = false;
				StopVisible = true;
				CurrentStatus = _coinJoiningMessage;
				coinJoinManager.Start(walletVm.Wallet);
			});

		_machine.Configure(State.ManualLoading)
			.Permit(Trigger.RoundStart, State.ManualPlaying);

		_machine.Configure(State.ManualFinished);


		_machine.OnTransitioned((trigger, source, destination) =>
		{
			Console.WriteLine($"Trigger: {trigger} caused state to change from: {source} to {destination}");
		});

		// AutoCj State
		_machine.Configure(State.AutoCoinJoin)
			.Permit(Trigger.AutoCoinJoinOff, State.ManualCoinJoin)
			.Permit(Trigger.AutoCoinJoinEntered, State.AutoStarting)
			.OnEntry(OnEnterAutoCoinJoin);

		_machine.Configure(State.AutoStarting)
			.SubstateOf(State.AutoCoinJoin)
			.Permit(Trigger.Pause, State.Paused)
			.Permit(Trigger.AutoPlayTimeout, State.AutoPlaying)
			.Permit(Trigger.Play, State.AutoPlaying)
			.OnEntry(() =>
			{
				_countDownStarted = DateTime.Now;
				IsAutoWaiting = true;
				_countDownMessage.Update();
				CurrentStatus = _countDownMessage;
			})
			.OnExit(() => IsAutoWaiting = false);

		_machine.Configure(State.Paused)
			.SubstateOf(State.AutoCoinJoin)
			.Permit(Trigger.Play, State.AutoPlaying)
			.OnEntry(() =>
			{
				coinJoinManager.Stop(walletVm.Wallet);
			});

		_machine.Configure(State.AutoPlaying)
			.SubstateOf(State.AutoCoinJoin)
			.Permit(Trigger.Pause, State.Paused)
			.Permit(Trigger.PlebStop, State.Paused)
			.Permit(Trigger.RoundComplete, State.AutoFinished)
			.Permit(Trigger.RoundComplete, State.AutoLoading)
			.OnEntry(()=>
			{
				IsAutoWaiting = false;
				PauseVisible = true;
				PlayVisible = false;
				CurrentStatus = _coinJoiningMessage;
				coinJoinManager.Start(walletVm.Wallet);
			});

		_machine.Configure(State.AutoLoading)
			.SubstateOf(State.AutoCoinJoin)
			.Permit(Trigger.RoundStart, State.AutoPlaying);

			_machine.Configure(State.AutoFinished)
				.SubstateOf(State.AutoCoinJoin)
				.Permit(Trigger.CoinReceive, State.AutoPlaying);

		PlayCommand = ReactiveCommand.Create(() => _machine.Fire(Trigger.Play));

		PauseCommand = ReactiveCommand.Create(() => _machine.Fire(Trigger.Pause));

		StopCommand = ReactiveCommand.Create(() => _machine.Fire(Trigger.Stop));

		DispatcherTimer.Run(() =>
		{
			OnTimerTick();
			return true;
		}, TimeSpan.FromSeconds(1));

		walletVm.Settings.WhenAnyValue(x => x.AutoCoinJoin)
			.Subscribe(SetAutoCoinJoin);

		_machine.Start();
	}

	private void StatusChanged(object? sender, StatusChangedEventArgs e)
	{
		switch (e)
		{
			case CoinJoinCompletedEventArgs coinJoinCompletedEventArgs:
				break;
			case LoadedEventArgs loadedEventArgs:
				break;
			case StartedEventArgs startedEventArgs:
				break;
			case StartErrorEventArgs startErrorEventArgs:
				break;
			case StopedEventArgs stopedEventArgs:
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(e));
		}

		Console.WriteLine($"CjStatus: {e.GetType()}");
	}

	public void SetAutoCoinJoin(bool enabled)
	{
		_machine.Fire(enabled ? Trigger.AutoCoinJoinOn : Trigger.AutoCoinJoinOff);
	}

	private void OnEnterStopped()
	{
		ProgressValue = 0;
		StopVisible = false;
		PlayVisible = true;

		CurrentStatus = _stoppedMessage;
	}

	private void OnEnterPlaying()
	{
		IsAutoWaiting = false;
		PauseVisible = IsAuto;
		StopVisible = !IsAuto;
		PlayVisible = false;
		CurrentStatus = _coinJoiningMessage;
	}

	private void OnEnterPause()
	{
		CurrentStatus = _pauseMessage;
		PauseVisible = false;
		PlayVisible = true;
		IsAutoWaiting = true;
	}

	private void OnExitAutoStarting()
	{
		IsAutoWaiting = false;
	}

	private void OnEnterAutoCoinJoin()
	{
		_autoStartTime = DateTime.Now + TimeSpan.FromSeconds(Random.Shared.Next(1 * 60, 2 * 60));
		_countDownMessage.Update();

		IsAuto = true;
		StopVisible = false;
		PauseVisible = false;
		PlayVisible = true;

		_machine.Fire(Trigger.AutoCoinJoinEntered);
	}

	private void OnEnterManualCoinJoin()
	{
		IsAuto = false;
		IsAutoWaiting = false;
		PlayVisible = true;
		StopVisible = false;
		PauseVisible = false;

		_machine.Fire(Trigger.ManualCoinJoinEntered);
	}

	private void OnTimerTick()
	{
		if (DateTime.Now > _autoStartTime)
		{
			_machine.Fire(Trigger.AutoPlayTimeout);
		}

		if (_machine.CurrentState == State.AutoStarting)
		{
			if (_autoStartTime > DateTimeOffset.Now)
			{
				var total = (_autoStartTime - _countDownStarted).TotalSeconds;

				var percentage = (DateTime.Now - _countDownStarted).TotalSeconds * 100 / total;
				ProgressValue = percentage;
			}
		}
	}

	public ICommand PlayCommand { get; }

	public ICommand PauseCommand { get; }

	public ICommand StopCommand { get; }
}
