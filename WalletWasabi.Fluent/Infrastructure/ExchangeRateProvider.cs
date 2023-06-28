using NBitcoin;
using ReactiveUI;
using WalletWasabi.Services;

namespace WalletWasabi.Fluent.Infrastructure;

public class ExchangeRateProvider : IExchangeRateProvider
{
	public ExchangeRateProvider(WasabiSynchronizer synchronizer)
	{
		Synchronizer = synchronizer;

		BtcToUsdRate = this.WhenAnyValue(x => x.Synchronizer.UsdExchangeRate);
	}

	private WasabiSynchronizer Synchronizer { get; }

	public IObservable<decimal> BtcToUsdRate { get; }


	public DualAmount Create(Money money)
	{
		return new DualAmount(money, this);
	}
}
