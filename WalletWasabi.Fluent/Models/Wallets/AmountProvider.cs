using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Services;

namespace WalletWasabi.Fluent.Models.Wallets;

public partial class AmountProvider : ReactiveObject
{
	[AutoNotify] private decimal _usdExchangeRate;

	public AmountProvider()
	{
		BtcToUsdExchangeRate = Services.Instance.EventBus
			.AsObservable<ExchangeRateChanged>()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Select(x => x.UsdBtcRate);

		BtcToUsdExchangeRate.Subscribe(x => UsdExchangeRate = x);

		UsdExchangeRate = Services.Instance.GetUsdExchangeRate();
	}

	public IObservable<decimal> BtcToUsdExchangeRate { get; }

	public Amount Create(Money? money)
	{
		return new Amount(money ?? Money.Zero, this);
	}
}
