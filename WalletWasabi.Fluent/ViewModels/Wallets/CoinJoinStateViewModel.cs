using System.Windows.Input;
using Avalonia.Threading;
using ReactiveUI;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.CoinJoin;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public partial class CoinJoinStateViewModel : ViewModelBase
{
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

		//_countDownMessage = new(() =>
		//	$"Coinjoin starts in {DateTime.Now - _walletCoinJoinManager.AutoCoinJoinStartTime:mm\\:ss}");

		//PlayCommand = ReactiveCommand.Create(() => _walletCoinJoinManager.Play());

		//PauseCommand = ReactiveCommand.Create(() => _walletCoinJoinManager.Pause());

		//StopCommand = ReactiveCommand.Create(() => _walletCoinJoinManager.Stop());

		DispatcherTimer.Run(() =>
		{
			OnTimerTick();
			return true;
		}, TimeSpan.FromSeconds(1));

		//WalletCoinJoinManagerOnStateChanged(this, _walletCoinJoinManager.WalletCoinjoinState);
	}

	private void WalletCoinJoinManagerOnStateChanged(object? sender, WalletCoinjoinState e)
	{
		Console.WriteLine($@"Entered state: {e}");

		/*if (IsAuto != _walletCoinJoinManager.AutoCoinJoin)
		{
			IsAuto = _walletCoinJoinManager.AutoCoinJoin;

			if (IsAuto)
			{
				OnEnterAutoCoinJoin();
			}
			else
			{
				OnEnterManualCoinJoin();
			}
		}*/

		switch (_lastState.Status)
		{
			case WalletCoinjoinState.State.AutoStarting:
				OnExitAutoStarting();
				break;

			case WalletCoinjoinState.State.LoadingTrack:
				break;

			case WalletCoinjoinState.State.Paused:
				break;

			case WalletCoinjoinState.State.Playing:

				break;

			case WalletCoinjoinState.State.Stopped:

				break;

			case WalletCoinjoinState.State.Finished:
				break;
		}

		var state = e;
		switch (state.Status)
		{
			case WalletCoinjoinState.State.AutoStarting:
				OnEnterAutoStarting(state);
				break;

			case WalletCoinjoinState.State.LoadingTrack:
				break;

			case WalletCoinjoinState.State.Paused:
				OnEnterPause();
				break;

			case WalletCoinjoinState.State.Playing:
				OnEnterPlaying();
				break;

			case WalletCoinjoinState.State.Stopped:
				OnEnterStopped();
				break;

			case WalletCoinjoinState.State.Finished:
				break;
		}
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
	}

	private void OnEnterManualCoinJoin()
	{
		IsAuto = false;
		IsAutoWaiting = false;
		PlayVisible = true;
		StopVisible = false;
		PauseVisible = false;
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
