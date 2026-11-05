using System.Reactive.Linq;
using NBitcoin;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Services;

namespace WalletWasabi.Fluent.Models.Wallets;

public partial class AmountProvider : ReactiveObject
{
	[AutoNotify] private decimal _exchangeRate;

	public AmountProvider(IServices services)
	{
		BtcToUsdExchangeRate = services.EventBus
			.AsObservable<ExchangeRateChanged>()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Select(x => x.ExchangeRate);

		BtcToUsdExchangeRate.Subscribe(x => ExchangeRate = x);

		ExchangeRate = services.GetExchangeRate();
	}

	public IObservable<decimal> BtcToUsdExchangeRate { get; }

	public Amount Create(Money? money)
	{
		return new Amount(money ?? Money.Zero, this);
	}
}
