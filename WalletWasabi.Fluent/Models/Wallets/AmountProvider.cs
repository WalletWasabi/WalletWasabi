using NBitcoin;
using ReactiveUI;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using WalletWasabi.Services;

namespace WalletWasabi.Fluent.Models.Wallets;

[AutoInterface]
public partial class AmountProvider : ReactiveObject
{
	[AutoNotify] private decimal _usdExchangeRate;

	public AmountProvider(WasabiSynchronizer synchronizer)
	{
		BtcToUsdExchangeRates = Observable.FromEventPattern<decimal>(synchronizer, nameof(Services.Synchronizer.UsdExchangeRateChanged))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Select(x => x.EventArgs);

		BtcToUsdExchangeRates.BindTo(this, x => x.UsdExchangeRate);
	}

	public IObservable<decimal> BtcToUsdExchangeRates { get; }

	public Amount Create(Money? money)
	{
		return new Amount(money ?? Money.Zero, this);
	}
}
