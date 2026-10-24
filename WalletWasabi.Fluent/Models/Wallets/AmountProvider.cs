using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Services;
using WalletWasabi.Wallets.Exchange;

namespace WalletWasabi.Fluent.Models.Wallets;

public partial class AmountProvider : ReactiveObject
{
	[AutoNotify] private ExchangeRate _exchangeRate;

	public AmountProvider(IServices services)
	{
		_exchangeRate = services.GetExchangeRate();

		BtcToFiatExchangeRate = services.EventBus
			.AsObservable<ExchangeRateChanged>()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Select(x => x.ExchangeRate);

		BtcToFiatExchangeRate.Subscribe(x => ExchangeRate = x);
	}

	public IObservable<ExchangeRate> BtcToFiatExchangeRate { get; }

	public Amount Create(Money? money)
	{
		return new Amount(money ?? Money.Zero, this);
	}
}
