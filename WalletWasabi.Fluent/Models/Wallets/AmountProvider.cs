using System.Reactive.Linq;
using NBitcoin;
using WalletWasabi.Client.Wallets.Exchange;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.Models.Wallets;

public partial class AmountProvider : ReactiveObject
{
	[AutoNotify] private ExchangeRate _exchangeRate;

	public AmountProvider(IServices services)
	{
		_exchangeRate = services.GetExchangeRate();

		BtcToFiatExchangeRate = services.EventBus
			.AsObservable<ExchangeRatesChanged>()
			.ToSignal()
			.Merge(services.UiConfig.WhenAnyValue(x => x.FiatCurrency).Skip(1).ToSignal())
			.ObserveOn(RxApp.MainThreadScheduler)
			.Select(_ => services.GetExchangeRate());

		BtcToFiatExchangeRate.Subscribe(x => ExchangeRate = x);
	}

	public IObservable<ExchangeRate> BtcToFiatExchangeRate { get; }

	public Amount Create(Money? money)
	{
		return new Amount(money ?? Money.Zero, this);
	}
}
