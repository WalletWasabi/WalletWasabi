using System.Windows.Input;
using Avalonia.Threading;
using ReactiveUI;
using Stateless;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

#nullable enable

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
	[AutoNotify] private MusicStatusMessageViewModel? _currentStatus;

	private readonly AutoUpdateMusicStatusMessageViewModel _countDownMessage;
	private readonly MusicStatusMessageViewModel _coinJoiningMessage = new() { Message = "Coinjoin in progress" };

	public CoinJoinStateViewModel(WalletViewModel walletVm)
	{
		_wallet = walletVm.Wallet;

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
			.Permit(Trigger.Play, State.ManualPlaying);

		_machine.Configure(State.ManualPlaying)
			.SubstateOf(State.ManualCoinJoin)
			.Permit(Trigger.Stop, State.Stopped)
			.Permit(Trigger.RoundComplete, State.ManualFinished)
			.Permit(Trigger.RoundComplete, State.ManualLoading);

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
			.OnEntry(()=> IsAuto = true)
			.InitialTransition(State.AutoStarting);

		_machine.Configure(State.AutoStarting)
			.SubstateOf(State.AutoCoinJoin)
			.Permit(Trigger.Pause, State.Paused)
			.Permit(Trigger.AutoPlayTimeout, State.AutoPlaying)
			.Permit(Trigger.Play, State.AutoPlaying)
			.OnEntry(() =>
			{
				IsAutoWaiting = true;
				_countDownMessage.Update();
				CurrentStatus = _countDownMessage;
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
			.OnEntry(()=> CurrentStatus = _coinJoiningMessage);

		_machine.Configure(State.AutoLoading)
			.SubstateOf(State.AutoCoinJoin)
			.Permit(Trigger.RoundStart, State.AutoPlaying)
			.OnEntry(OnLoading);

		_machine.Configure(State.AutoFinished)
			.SubstateOf(State.AutoCoinJoin)
			.Permit(Trigger.CoinReceive, State.AutoPlaying)
			.OnEntry(OnFinished);

		_machine.OnTransitioned(OnStateTransition);

		PlayCommand = ReactiveCommand.Create(Play);

		PauseCommand = ReactiveCommand.Create(Pause);

		_coinJoinManager = Services.HostedServices.Get<CoinJoinManager>();

		_countDownMessage = new(() =>
			$"CoinJoin will auto-start in: {DateTime.Now - _coinJoinManager.WhenWalletCanStartAutoCoinJoin(_wallet):mm\\:ss}");

		DispatcherTimer.Run(() =>
		{
			OnTimerTick();
			return true;
		}, TimeSpan.FromSeconds(1));

		walletVm.Settings.WhenAnyValue(x => x.AutoCoinJoin)
			.Subscribe(SetAutoCoinJoin);
	}

	private void OnEnterManualCoinJoin()
	{
		IsAuto = false;
		IsAutoWaiting = false;
	}

	private void OnStateTransition(StateMachine<State, Trigger>.Transition transition)
	{
		Console.WriteLine($@"Event: {transition.Trigger} triggered: State changed from: {transition.Source} to: {transition.Destination}");
	}

	public State CurrentState => _machine.State;

	public bool IsInState(State state) => _machine.IsInState(state);

	private void OnTimerTick()
	{
		if (_coinJoinManager.WhenWalletCanStartAutoCoinJoin(_wallet) < DateTimeOffset.Now)
		{
			_machine.Fire(Trigger.AutoPlayTimeout);
		}
	}

	private void OnExitManualCoinJoin()
	{
		Console.WriteLine("Exiting manual");
	}

	private void OnPause()
	{
	}

	private void OnPauseFromPlebStop()
	{
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

	public void Play()
	{
		_machine.Fire(Trigger.Play);
	}

	public void Pause()
	{
		_machine.Fire(Trigger.Pause);
	}

	public void PlebStop()
	{
		_machine.Fire(Trigger.PlebStop);
	}

	public void AutoStartTimeout()
	{
		if (_machine.State == State.AutoStarting)
		{
			_machine.Fire(Trigger.AutoPlayTimeout);
		}
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