using System.Windows.Input;
using Avalonia.Threading;
using ReactiveUI;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.CoinJoin;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public partial class CoinJoinStateViewModel : ViewModelBase
{
	private readonly WalletCoinJoinManager _walletCoinJoinManager;

	[AutoNotify] private bool _isAutoWaiting;
	[AutoNotify] private bool _isAuto;
	[AutoNotify] private bool _playVisible = true;
	[AutoNotify] private bool _pauseVisible;
	[AutoNotify] private bool _stopVisible;
	[AutoNotify] private MusicStatusMessageViewModel? _currentStatus;
	[AutoNotify] private bool _isProgressReversed;
	[AutoNotify] private double _progressValue;

	private readonly AutoUpdateMusicStatusMessageViewModel _countDownMessage;
	private readonly MusicStatusMessageViewModel _coinJoiningMessage = new() { Message = "Coinjoin in progress" };

	private readonly MusicStatusMessageViewModel _plebStopMessage = new()
		{ Message = "Auto Coinjoin paused, due to PlebStop" };

	private readonly MusicStatusMessageViewModel _pauseMessage = new() { Message = "Auto Coinjoin is paused" };
	private readonly MusicStatusMessageViewModel _stoppedMessage = new() { Message = "Coinjoin is stopped" };
	private readonly DateTime _countDownStartTime;
	private IWalletCoinJoinState _lastState = new Stopped();

	public CoinJoinStateViewModel(WalletViewModel walletVm)
	{
		_countDownStartTime = DateTime.Now;

		var coinJoinManager = Services.HostedServices.Get<CoinJoinManager>();

		_walletCoinJoinManager = coinJoinManager.WalletCoinJoinManagers[walletVm.WalletName];

		_walletCoinJoinManager.StateChanged += WalletCoinJoinManagerOnStateChanged;

		_countDownMessage = new(() =>
			$"CoinJoin will auto-start in: {DateTime.Now - _walletCoinJoinManager.AutoCoinJoinStartTime:mm\\:ss}");


		PlayCommand = ReactiveCommand.Create(() => _walletCoinJoinManager.Play());

		PauseCommand = ReactiveCommand.Create(() => _walletCoinJoinManager.Pause());

		StopCommand = ReactiveCommand.Create(() => _walletCoinJoinManager.Stop());

		DispatcherTimer.Run(() =>
		{
			OnTimerTick();
			return true;
		}, TimeSpan.FromSeconds(1));

		WalletCoinJoinManagerOnStateChanged(this, _walletCoinJoinManager.WalletCoinJoinState);
	}

	private void WalletCoinJoinManagerOnStateChanged(object? sender, IWalletCoinJoinState e)
	{
		Console.WriteLine($@"Entered state: {e}");

		if (IsAuto != _walletCoinJoinManager.AutoCoinJoin)
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
		}

		switch (_lastState)
		{
			case AutoStarting autoStarting:
				OnExitAutoStarting();
				break;

			case LoadingTrack loadingTrack:
				break;

			case Paused paused:
				break;

			case Playing playing:

				break;

			case Stopped stopped:

				break;

			case Finished finished:
				break;
		}

		switch (e)
		{
			case AutoStarting autoStarting:
				OnEnterAutoStarting(autoStarting);
				break;

			case LoadingTrack loadingTrack:
				break;

			case Paused paused:
				OnEnterPause();
				break;

			case Playing playing:
				OnEnterPlaying();
				break;

			case Stopped stopped:
				OnEnterStopped();
				break;

			case Finished finished:
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

	private void OnEnterAutoStarting(AutoStarting autoStarting)
	{
		IsAutoWaiting = true;
		_countDownMessage.Update();
		CurrentStatus = _countDownMessage;

		if (autoStarting.IsPlebStop)
		{
			CurrentStatus = _plebStopMessage;
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
		if (_walletCoinJoinManager.WalletCoinJoinState is AutoStarting)
		{
			var whenCanAutoStart = _walletCoinJoinManager.AutoCoinJoinStartTime;

			if (whenCanAutoStart > DateTimeOffset.Now)
			{
				var end = whenCanAutoStart;
				var total = (end - _countDownStartTime).TotalSeconds;

				var percentage = (DateTime.Now - _countDownStartTime).TotalSeconds * 100 / total;
				ProgressValue = percentage;
			}
		}
	}

	public ICommand PlayCommand { get; }

	public ICommand PauseCommand { get; }

	public ICommand StopCommand { get; }
}