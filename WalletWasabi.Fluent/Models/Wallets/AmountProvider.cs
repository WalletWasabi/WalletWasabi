using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Services;

namespace WalletWasabi.Fluent.Models.Wallets;

[AutoInterface]
public partial class AmountProvider : ReactiveObject
{
	[AutoNotify] private decimal _usdExchangeRate;

	public AmountProvider()
	{
		BtcToUsdExchangeRate = Services.EventBus
			.AsObservable<ExchangeRateChanged>()
			.Select(e => e.UsdBtcRate);

		BtcToUsdExchangeRate.BindTo(this, x => x.UsdExchangeRate);
	}

	public IObservable<decimal> BtcToUsdExchangeRate { get; }

	public Amount Create(Money? money)
	{
		return new Amount(money ?? Money.Zero, this);
	}
}
