using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Services;

namespace WalletWasabi.Fluent.ViewModels;

public class ExchangeRateViewModel : ViewModelBase
{
	public ExchangeRateViewModel(WasabiSynchronizer synchronizer)
	{
		var usdExchangeRate = synchronizer
			.WhenAnyValue(x => x.UsdExchangeRate);

		BtcToUsdExchangeRate = usdExchangeRate
			.Select(usd => usd.FormattedFiat("N0") + " USD")
			.ObserveOn(RxApp.MainThreadScheduler);
	}

	public IObservable<string> BtcToUsdExchangeRate { get; }
}