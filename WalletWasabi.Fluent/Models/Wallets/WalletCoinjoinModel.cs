using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.StatusChangedEvents;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

[AutoInterface]
public partial class WalletCoinjoinModel
{
	private readonly Wallet _wallet;
	private CoinJoinManager _coinJoinManager;

	public WalletCoinjoinModel(Wallet wallet, IWalletSettingsModel settings)
	{
		_wallet = wallet;
		_coinJoinManager = Services.HostedServices.Get<CoinJoinManager>();

		StatusUpdated =
			Observable.FromEventPattern<StatusChangedEventArgs>(_coinJoinManager, nameof(CoinJoinManager.StatusChanged))
					  .Where(x => x.EventArgs.Wallet == wallet)
					  .Select(x => x.EventArgs)
					  .Where(x => x is WalletStartedCoinJoinEventArgs or WalletStoppedCoinJoinEventArgs or StartErrorEventArgs or CoinJoinStatusEventArgs)
					  .ObserveOn(RxApp.MainThreadScheduler);

		settings.WhenAnyValue(x => x.AutoCoinjoin)
				.Skip(1) // The first one is triggered at the creation.
				.DoAsync(async (autoCoinJoin) =>
				{
					if (autoCoinJoin)
					{
						await StartAsync(stopWhenAllMixed: false, false);
					}
					else
					{
						await StopAsync();
					}
				})
				.Subscribe();
	}

	public IObservable<StatusChangedEventArgs> StatusUpdated { get; }

	public async Task StartAsync(bool stopWhenAllMixed, bool overridePlebStop)
	{
		await _coinJoinManager.StartAsync(_wallet, stopWhenAllMixed, overridePlebStop, CancellationToken.None);
	}

	public async Task StopAsync()
	{
		await _coinJoinManager.StopAsync(_wallet, CancellationToken.None);
	}
}
