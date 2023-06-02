using ReactiveUI;
using WalletWasabi.Services;

namespace WalletWasabi.Fluent.Infrastructure;

public class ObservableExchangeRateProvider : IObservableExchangeRateProvider
{
	public ObservableExchangeRateProvider(WasabiSynchronizer synchronizer)
	{
		Synchronizer = synchronizer;

		BtcToUsdRate = this.WhenAnyValue(x => x.Synchronizer.UsdExchangeRate);
	}

	private WasabiSynchronizer Synchronizer { get; }

	public IObservable<decimal> BtcToUsdRate { get; }
}
