using System.Threading;
using System.Windows.Input;
using Avalonia.Threading;
using ReactiveUI;
using Stateless;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public partial class CoinJoinStateViewModel : ViewModelBase
{
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

	private enum Trigger
	{
		AutoCoinJoinOn,
		AutoCoinJoinOff,
		Pause,
		Play,
		Stop,
		AutoPlayTimeout,
		PlebStop,
		CoinReceive,
		RoundComplete,
		RoundStart,
	}

	private readonly StateMachine<State, Trigger> _machine;
	private readonly CoinJoinManager _coinJoinManager;
	private readonly Wallet _wallet;

	[AutoNotify] private bool _isAutoWaiting;
	[AutoNotify] private bool _isAuto;
	[AutoNotify] private bool _playVisible = true;
	[AutoNotify] private bool _pauseVisible;
	[AutoNotify] private bool _stopVisible;
	[AutoNotify] private MusicStatusMessageViewModel? _currentStatus;
	[AutoNotify] private bool _isProgressReversed;
	[AutoNotify] private double _progressValue;

	private readonly MusicStatusMessageViewModel _coinJoiningMessage = new() { Message = "Coinjoin in progress" };
	private readonly MusicStatusMessageViewModel _plebStopMessage = new() { Message = "Auto Coinjoin paused, due to PlebStop" };
	private readonly MusicStatusMessageViewModel _pauseMessage = new() { Message = "Auto Coinjoin is paused" };
	private readonly MusicStatusMessageViewModel _stoppedMessage = new() { Message = "Coinjoin is stopped" };
	private readonly DateTime _countDownStartTime;
	private DateTime _playStartTime;

	public CoinJoinStateViewModel(WalletViewModel walletVm)
	{
		_wallet = walletVm.Wallet;
		_countDownStartTime = DateTime.Now;

		_coinJoinManager = Services.HostedServices.Get<CoinJoinManager>();

		AutoUpdateMusicStatusMessageViewModel countDownMessage = new(() =>
			$"CoinJoin will auto-start in: {DateTime.Now - _coinJoinManager.WhenWalletCanStartAutoCoinJoin(_wallet):mm\\:ss}");

		_machine = new StateMachine<State, Trigger>(walletVm.Settings.AutoCoinJoin
			? State.AutoCoinJoin
			: State.ManualCoinJoin);

		// See diagram in the developer docs.
		// Manual Cj State
		_machine.Configure(State.ManualCoinJoin)
			.Permit(Trigger.AutoCoinJoinOn, State.AutoCoinJoin)
			.PermitReentry(Trigger.AutoCoinJoinOff)
			.InitialTransition(State.Stopped)
			.OnEntry(OnEnterManualCoinJoin)
			.OnExit(OnExitManualCoinJoin);

		_machine.Configure(State.Stopped)
			.SubstateOf(State.ManualCoinJoin)
			.OnEntry(()=>
			{
				ProgressValue = 0;
				StopVisible = false;
				PlayVisible = true;
				_wallet.AllowManualCoinJoin = false;
				CurrentStatus = _stoppedMessage;
			})
			.Permit(Trigger.Play, State.ManualPlaying);

		_machine.Configure(State.ManualPlaying)
			.SubstateOf(State.ManualCoinJoin)
			.Permit(Trigger.Stop, State.Stopped)
			.Permit(Trigger.RoundComplete, State.ManualFinished)
			.Permit(Trigger.RoundComplete, State.ManualLoading)
			.OnEntry(() =>
			{
				_playStartTime = DateTime.Now;
				PlayVisible = false;
				StopVisible = true;
				_wallet.AllowManualCoinJoin = true;
				CurrentStatus = _coinJoiningMessage;
			});

		_machine.Configure(State.ManualLoading)
			.SubstateOf(State.ManualCoinJoin)
			.Permit(Trigger.RoundStart, State.ManualPlaying);

		_machine.Configure(State.ManualFinished)
			.SubstateOf(State.ManualCoinJoin)
			.InitialTransition(State.Stopped);


		// AutoCj State
		_machine.Configure(State.AutoCoinJoin)
			.Permit(Trigger.AutoCoinJoinOff, State.ManualCoinJoin)
			.PermitReentry(Trigger.AutoCoinJoinOn)
			.OnEntry(()=>
			{
				IsAuto = true;
				StopVisible = false;
				PlayVisible = true;
				PauseVisible = false;
			})
			.InitialTransition(State.AutoStarting);

		_machine.Configure(State.AutoStarting)
			.SubstateOf(State.AutoCoinJoin)
			.Permit(Trigger.Pause, State.Paused)
			.Permit(Trigger.AutoPlayTimeout, State.AutoPlaying)
			.Permit(Trigger.Play, State.AutoPlaying)
			.OnEntry(() =>
			{
				IsAutoWaiting = true;
				countDownMessage.Update();
				CurrentStatus = countDownMessage;
			})
			.OnExit(() => IsAutoWaiting = false);

		_machine.Configure(State.Paused)
			.SubstateOf(State.AutoCoinJoin)
			.Permit(Trigger.Play, State.AutoPlaying)
			.OnEntryFrom(Trigger.PlebStop, OnPauseFromPlebStop)
			.OnEntryFrom(Trigger.Pause, OnPause);

		_machine.Configure(State.AutoPlaying)
			.SubstateOf(State.AutoCoinJoin)
			.Permit(Trigger.Pause, State.Paused)
			.Permit(Trigger.PlebStop, State.Paused)
			.Permit(Trigger.RoundComplete, State.AutoFinished)
			.Permit(Trigger.RoundComplete, State.AutoLoading)
			.OnEntry(()=>
			{
				_playStartTime = DateTime.Now;
				IsAutoWaiting = false;
				PauseVisible = true;
				PlayVisible = false;
				CurrentStatus = _coinJoiningMessage;
			});

		_machine.Configure(State.AutoLoading)
			.SubstateOf(State.AutoCoinJoin)
			.Permit(Trigger.RoundStart, State.AutoPlaying)
			.OnEntry(OnLoading);

		_machine.Configure(State.AutoFinished)
			.SubstateOf(State.AutoCoinJoin)
			.Permit(Trigger.CoinReceive, State.AutoPlaying)
			.OnEntry(OnFinished);

		_machine.OnTransitioned(OnStateTransition);

		PlayCommand = ReactiveCommand.Create(()=>_machine.Fire(Trigger.Play));

		PauseCommand = ReactiveCommand.Create(()=>_machine.Fire(Trigger.Pause));

		StopCommand = ReactiveCommand.Create(() => _machine.Fire(Trigger.Stop));

		DispatcherTimer.Run(() =>
		{
			OnTimerTick();
			return true;
		}, TimeSpan.FromSeconds(1));

		walletVm.Settings.WhenAnyValue(x => x.AutoCoinJoin)
			.Subscribe(SetAutoCoinJoin);

		_coinJoinManager.RoundStatusUpdater.CreateRoundAwaiter(OnRoundStatusUpdated, CancellationToken.None);
	}

	private bool OnRoundStatusUpdated(RoundState roundState)
	{
		if (_machine.State == State.AutoPlaying || _machine.State == State.ManualPlaying)
		{
			var timeout = roundState.ConnectionConfirmationTimeout;
			var total = timeout.TotalSeconds;

			var percentage = (DateTime.Now - _playStartTime).TotalSeconds * 100 / total;
			ProgressValue = percentage;
		}

		return true;
	}

	private void OnEnterManualCoinJoin()
	{
		IsAuto = false;
		IsAutoWaiting = false;
		PlayVisible = true;
		StopVisible = false;
		PauseVisible = false;
	}

	private void OnStateTransition(StateMachine<State, Trigger>.Transition transition)
	{
		Console.WriteLine($@"Event: {transition.Trigger} triggered: State changed from: {transition.Source} to: {transition.Destination}");
	}

	private void OnTimerTick()
	{
		if (_machine.State == State.AutoStarting)
		{
			var whenCanAutoStart = _coinJoinManager.WhenWalletCanStartAutoCoinJoin(_wallet);

			if (whenCanAutoStart < DateTimeOffset.Now)
			{
				_machine.Fire(Trigger.AutoPlayTimeout);
			}
			else
			{
				var end = whenCanAutoStart;
				var total = (end - _countDownStartTime).TotalSeconds;

				var percentage = (DateTime.Now - _countDownStartTime).TotalSeconds * 100 / total;
				ProgressValue = percentage;
			}
		}
	}

	private void OnExitManualCoinJoin()
	{
		Console.WriteLine("Exiting manual");
	}

	private void OnPause()
	{
		CurrentStatus = _pauseMessage;
		PauseVisible = false;
		PlayVisible = true;
		IsAutoWaiting = true;
	}

	private void OnPauseFromPlebStop()
	{
		OnPause();

		CurrentStatus = _plebStopMessage;
	}

	private void OnLoading()
	{
	}

	private void OnFinished()
	{
	}

	public void SetAutoCoinJoin(bool enabled)
	{
		_machine.Fire(enabled ? Trigger.AutoCoinJoinOn : Trigger.AutoCoinJoinOff);
	}

	public void PlebStop()
	{
		_machine.Fire(Trigger.PlebStop);
	}


	public void RoundComplete()
	{
		_machine.Fire(Trigger.RoundComplete);
	}

	public void ReceivedUnmixedCoins()
	{
		_machine.Fire(Trigger.CoinReceive);
	}

	public void StartNewRound()
	{
		_machine.Fire(Trigger.RoundStart);
	}

	public ICommand PlayCommand { get; }

	public ICommand PauseCommand { get; }

	public ICommand StopCommand { get; }
}