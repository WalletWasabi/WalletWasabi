using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public enum CoinJoinUIState
{
	Stopped,
	AutoStart,
	Starting,
	Started,
	Pausing,
	Paused,
	Stopping,
}

public partial class MusicControlsViewModel : ViewModelBase
{
	[AutoNotify (SetterModifier = AccessModifier.Private)] private bool _isActive;
	[AutoNotify] private TimeSpan _autoStartCountdown;
	[AutoNotify] private string? _currentStatus;
	[AutoNotify] private string? _countDown;
	[AutoNotify] private bool _playVisible = true;
	[AutoNotify] private bool _pauseVisible;
	[AutoNotify] private bool _stopVisible;

	private CoinJoinManager? _coinJoinManager;
	private Wallet? _currentWallet;
	private CoinJoinUIState _currentState;

	public MusicControlsViewModel()
	{
		_currentState = CoinJoinUIState.Stopped;

		Observable.Interval(TimeSpan.FromSeconds(1))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(x => UpdateStatus());

		PlayCommand = ReactiveCommand.Create(() =>
		{
			if (_currentWallet is { })
			{
				PlayVisible = false;
				_currentWallet.AllowManualCoinJoin = true;

				_currentState = CoinJoinUIState.Starting;

			}
		});

		PauseCommand = ReactiveCommand.Create(() =>
		{

		});

		StopCommand = ReactiveCommand.Create(() =>
		{
			if (_currentWallet is { })
			{
				_currentWallet.AllowManualCoinJoin = false;

				_currentState = CoinJoinUIState.Stopping;
			}
		});
	}

	public ICommand PlayCommand { get; }

	public ICommand PauseCommand { get; }

	public ICommand StopCommand { get; }

	public void Initialize()
	{
		_coinJoinManager = Services.HostedServices.Get<CoinJoinManager>();

		_coinJoinManager.WalletStatusChanged += CoinJoinManagerOnWalletStatusChanged;

		_coinJoinManager.RoundStatusUpdater.CreateRoundAwaiter(OnRoundStatusUpdated, CancellationToken.None);
	}

	private bool OnRoundStatusUpdated(RoundState state)
	{

		return true;
	}

	private void CoinJoinManagerOnWalletStatusChanged(object? sender, WalletStatusChangedEventArgs e)
	{
		if (e.Wallet == _currentWallet)
		{

		}
	}

	public IDisposable SetWallet(Wallet wallet)
	{
		_currentWallet = wallet;

		_currentState = CoinJoinUIState.AutoStart;

		UpdateStatus();

		IsActive = true;

		return Disposable.Create(() => IsActive = false);
	}

	private void UpdateStatus()
	{
		if (_currentWallet is { })
		{
			CountDown = null;
			switch (_currentState)
			{
				case CoinJoinUIState.AutoStart:
					CurrentStatus =
						$"CoinJoin will auto-start in: {(DateTime.Now - _coinJoinManager.WhenWalletCanStartAutoCoinJoin(_currentWallet)):mm\\:ss}";

					CountDown =
						$"{(DateTime.Now - _coinJoinManager.WhenWalletCanStartAutoCoinJoin(_currentWallet)):mm\\:ss}";
					break;

				case CoinJoinUIState.Starting:
					CurrentStatus =
						$"Initialising CoinJoin";
					break;

			}

		}
	}
}