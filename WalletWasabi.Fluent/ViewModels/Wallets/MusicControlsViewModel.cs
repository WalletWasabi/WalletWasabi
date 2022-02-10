using System.Reactive.Linq;
using Avalonia.Threading;
using ReactiveUI;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public partial class MusicControlsViewModel : ViewModelBase
{
	[AutoNotify] private bool _isActive;
	[AutoNotify] private TimeSpan _autoStartCountdown;
	[AutoNotify] private string? _currentStatus;


	public MusicControlsViewModel(Wallet wallet)
	{
		IsActive = true;

		var coinJoinManager = Services.HostedServices.Get<CoinJoinManager>();

		Observable.Interval(TimeSpan.FromSeconds(1))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(x =>
			{
				CurrentStatus = $"CoinJoin will auto-start in this is a very long message: {(DateTime.Now - coinJoinManager.WhenWalletCanStartAutoCoinJoin(wallet)):mm\\:ss}";



			});
	}
}