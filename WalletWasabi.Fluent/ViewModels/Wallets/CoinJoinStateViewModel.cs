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
	RoundStartFailed,
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
	[AutoNotify] private string _elapsedTime;
	[AutoNotify] private string _remainingTime;

	private readonly MusicStatusMessageViewModel _countDownMessage = new()
		{ Message = "Waiting to auto-start coinjoin" };

	private readonly MusicStatusMessageViewModel _coinJoiningMessage = new() { Message = "Coinjoining" };

	private readonly MusicStatusMessageViewModel _pauseMessage = new()
		{ Message = "Coinjoin is paused until the next round" };

	private readonly MusicStatusMessageViewModel _stoppedMessage = new() { Message = "Coinjoin is stopped" };
	private readonly MusicStatusMessageViewModel _startErrorMessage = new() { };
	private DateTime _autoStartTime;
	private DateTime _countDownStarted;

	public CoinJoinStateViewModel(WalletViewModel walletVm)
	{
		var coinJoinManager = Services.HostedServices.Get<CoinJoinManager>();

		coinJoinManager.StatusChanged += StatusChanged;

		_machine =
			new StateMachine<State, Trigger>(walletVm.Settings.AutoCoinJoin
				? State.AutoCoinJoin
				: State.ManualCoinJoin);


		// See diagram in the developer docs.
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
			});

		_machine.Configure(State.Stopped)
			.SubstateOf(State.ManualCoinJoin)
			.OnEntry(() =>
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
			.Permit(Trigger.RoundStartFailed, State.ManualFinished)
			.Permit(Trigger.RoundStart, State.ManualLoading)
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
			.OnEntry(() =>
			{
				_autoStartTime = DateTime.Now + TimeSpan.FromSeconds(Random.Shared.Next(1 * 60, 2 * 60));

				IsAuto = true;
				StopVisible = false;
				PauseVisible = false;
				PlayVisible = true;

				_machine.Fire(Trigger.AutoCoinJoinEntered);
			});

		_machine.Configure(State.AutoStarting)
			.SubstateOf(State.AutoCoinJoin)
			.Permit(Trigger.Pause, State.Paused)
			.Permit(Trigger.AutoPlayTimeout, State.AutoPlaying)
			.Permit(Trigger.Play, State.AutoPlaying)
			.OnEntry(() =>
			{
				_countDownStarted = DateTime.Now;
				IsAutoWaiting = true;
				CurrentStatus = _countDownMessage;
			})
			.OnProcess(() =>
			{
				ElapsedTime = $"{DateTime.Now - _countDownStarted:mm\\:ss}";
				RemainingTime = $"-{_autoStartTime - DateTime.Now:mm\\:ss}";

				var total = (_autoStartTime - _countDownStarted).TotalSeconds;
				var percentage = (DateTime.Now - _countDownStarted).TotalSeconds * 100 / total;
				ProgressValue = percentage;

				if (DateTime.Now > _autoStartTime)
				{
					_machine.Fire(Trigger.AutoPlayTimeout);
				}
			})
			.OnExit(() => IsAutoWaiting = false);

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
			});

		_machine.Configure(State.AutoPlaying)
			.SubstateOf(State.AutoCoinJoin)
			.Permit(Trigger.Pause, State.Paused)
			.Permit(Trigger.PlebStop, State.Paused)
			.Permit(Trigger.RoundStartFailed, State.AutoFinished)
			.Permit(Trigger.RoundStart, State.AutoPlaying)
			.OnEntry(() =>
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
			.Permit(Trigger.CoinReceive, State.AutoPlaying)
			.OnEntry(() =>
			{
				PauseVisible = false;
				IsAutoWaiting = true;

				_startErrorMessage.Message = "Coinjoin stopped. No coins to mix.";

				CurrentStatus = _startErrorMessage;
			});

		PlayCommand = ReactiveCommand.Create(() => _machine.Fire(Trigger.Play));

		PauseCommand = ReactiveCommand.Create(() => _machine.Fire(Trigger.Pause));

		StopCommand = ReactiveCommand.Create(() => _machine.Fire(Trigger.Stop));

		DispatcherTimer.Run(() =>
		{
			_machine.Process();
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
				// TODO implement a message to show success / failure.
				break;
			case StartedEventArgs startedEventArgs:
				_machine.Fire(Trigger.RoundStart);
				break;

			case StartErrorEventArgs startErrorEventArgs:
				_machine.Fire(Trigger.RoundStartFailed);
				break;

			case StoppedEventArgs stoppedEventArgs:
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

	public ICommand PlayCommand { get; }

	public ICommand PauseCommand { get; }

	public ICommand StopCommand { get; }
}