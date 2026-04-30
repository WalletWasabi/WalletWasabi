using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Services;

namespace WalletWasabi.Fluent.Models.Wallets;

public partial class AmountProvider : ReactiveObject
{
	[AutoNotify] private decimal _usdExchangeRate;

	public AmountProvider(IServices services)
	{
		BtcToUsdExchangeRate = services.EventBus
			.AsObservable<ExchangeRateChanged>()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Select(x => x.UsdBtcRate);

		BtcToUsdExchangeRate.Subscribe(x => UsdExchangeRate = x);

		UsdExchangeRate = services.GetUsdExchangeRate();
	}

	public IObservable<decimal> BtcToUsdExchangeRate { get; }

	public Amount Create(Money? money)
	{
		return new Amount(money ?? Money.Zero, this);
	}
}
