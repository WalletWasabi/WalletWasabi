using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Services;

namespace WalletWasabi.Fluent.Models.Wallets;

public interface IAmountProvider
{
	decimal UsdExchangeRate { get; set; }

	IObservable<decimal> BtcToUsdExchangeRate { get; }

	Amount Create(Money? money);
}

public partial class AmountProvider : ReactiveObject, IAmountProvider
{
	[AutoNotify] private decimal _usdExchangeRate;

	public AmountProvider()
	{
		BtcToUsdExchangeRate = Services.EventBus
			.AsObservable<ExchangeRateChanged>()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Select(x => x.UsdBtcRate);

		BtcToUsdExchangeRate.Subscribe(x => UsdExchangeRate = x);

		UsdExchangeRate = Services.Status.UsdExchangeRate;
	}

	public IObservable<decimal> BtcToUsdExchangeRate { get; }

	public Amount Create(Money? money)
	{
		return new Amount(money ?? Money.Zero, this);
	}
}
