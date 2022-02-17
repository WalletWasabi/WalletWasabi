using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public partial class MusicControlsViewModel : ViewModelBase
{
	[AutoNotify(SetterModifier = AccessModifier.Private)]
	private bool _isActive;

	[AutoNotify] private TimeSpan _autoStartCountdown;
	[AutoNotify] private string? _countDown;
	[AutoNotify] private bool _playVisible = true;
	[AutoNotify] private bool _pauseVisible;
	[AutoNotify] private bool _stopVisible;

	private CoinJoinManager? _coinJoinManager;
	[AutoNotify] private WalletViewModel? _currentWallet;


	public MusicControlsViewModel()
	{

	}

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
		if (e.Wallet == _currentWallet?.Wallet)
		{
		}
	}

	public IDisposable SetWallet(WalletViewModel wallet)
	{
		CurrentWallet = wallet;

		IsActive = true;

		return new CompositeDisposable(
			Disposable.Create(() => IsActive = false));
	}
}