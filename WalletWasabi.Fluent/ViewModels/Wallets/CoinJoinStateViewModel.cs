using System.Collections.Generic;
using System.Windows.Input;
using Avalonia.Threading;
using ReactiveUI;
using WalletWasabi.Fluent.State;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.CoinJoin;

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
	private readonly DateTime _countDownStartTime;
	private WalletCoinjoinState _lastState = WalletCoinjoinState.Stopped();

	public CoinJoinStateViewModel(WalletViewModel walletVm)
	{
		_countDownStartTime = DateTime.Now;

		var coinJoinManager = Services.HostedServices.Get<CoinJoinManager>();

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
				//_wallet.AllowManualCoinJoin = false;
				CurrentStatus = _stoppedMessage;
				coinJoinManager.Stop(walletVm.WalletName);
			})
			.Permit(Trigger.Play, State.ManualPlaying);

		_machine.Configure(State.ManualPlaying)
			.Permit(Trigger.Stop, State.Stopped)
			.Permit(Trigger.RoundComplete, State.ManualFinished)
			.Permit(Trigger.RoundComplete, State.ManualLoading)
			.OnEntry(() =>
			{
				//_playStartTime = DateTime.Now;
				PlayVisible = false;
				StopVisible = true;
				//_wallet.AllowManualCoinJoin = true;
				CurrentStatus = _coinJoiningMessage;
				coinJoinManager.Start(walletVm.WalletName);
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
			.Permit(Trigger.AutoCoinJoinEntered, State.Paused)
			.OnEntry(OnEnterAutoCoinJoin);

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
			.Permit(Trigger.Play, State.AutoPlaying);
			//.OnEntryFrom(Trigger.PlebStop, OnPauseFromPlebStop)
			//.OnEntryFrom(Trigger.Pause, OnPause);

		_machine.Configure(State.AutoPlaying)
			.SubstateOf(State.AutoCoinJoin)
			.Permit(Trigger.Pause, State.Paused)
			.Permit(Trigger.PlebStop, State.Paused)
			.Permit(Trigger.RoundComplete, State.AutoFinished)
			.Permit(Trigger.RoundComplete, State.AutoLoading)
			.OnEntry(()=>
			{
				//_playStartTime = DateTime.Now;
				IsAutoWaiting = false;
				PauseVisible = true;
				PlayVisible = false;
				CurrentStatus = _coinJoiningMessage;
			});

		_machine.Configure(State.AutoLoading)
			.SubstateOf(State.AutoCoinJoin)
			.Permit(Trigger.RoundStart, State.AutoPlaying);
			//.OnEntry(OnLoading);

			_machine.Configure(State.AutoFinished)
				.SubstateOf(State.AutoCoinJoin)
				.Permit(Trigger.CoinReceive, State.AutoPlaying);
			//.OnEntry(OnFinished);


		/*

		//_countDownMessage = new(() =>
		//	$"Coinjoin starts in {DateTime.Now - _walletCoinJoinManager.AutoCoinJoinStartTime:mm\\:ss}");*/

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

		//WalletCoinJoinManagerOnStateChanged(this, _walletCoinJoinManager.WalletCoinjoinState);
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

	private void OnEnterAutoStarting(WalletCoinjoinState autoStarting)
	{
		if (autoStarting.Status != WalletCoinjoinState.State.AutoStarting)
		{
			throw new InvalidOperationException($"{nameof(autoStarting.Status)} must be {nameof(WalletCoinjoinState.State.AutoStarting)}.");
		}
		IsAutoWaiting = true;
		_countDownMessage.Update();
		CurrentStatus = _countDownMessage;

		if (autoStarting.IsPlebStop)
		{
			CurrentStatus = _pauseMessage;
		}
	}

	private void OnExitAutoStarting()
	{
		IsAutoWaiting = false;
	}

	private void OnEnterAutoCoinJoin()
	{
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
		/*if (_walletCoinJoinManager.WalletCoinjoinState.Status == WalletCoinjoinState.State.AutoStarting)
		{
			var whenCanAutoStart = _walletCoinJoinManager.AutoCoinJoinStartTime;

			if (whenCanAutoStart > DateTimeOffset.Now)
			{
				var end = whenCanAutoStart;
				var total = (end - _countDownStartTime).TotalSeconds;

				var percentage = (DateTime.Now - _countDownStartTime).TotalSeconds * 100 / total;
				ProgressValue = percentage;
			}
		}*/
	}

	public ICommand PlayCommand { get; }

	public ICommand PauseCommand { get; }

	public ICommand StopCommand { get; }
}
