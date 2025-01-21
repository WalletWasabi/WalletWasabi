using NBitcoin;
using ReactiveUI;
using WalletWasabi.Wallets.Exchange;

namespace WalletWasabi.Fluent.Models.Wallets;

[AutoInterface]
public partial class AmountProvider : ReactiveObject
{
	private readonly ExchangeRateUpdater _exchangeRateUpdater;
	[AutoNotify] private decimal _usdExchangeRate;

	public AmountProvider(ExchangeRateUpdater exchangeRateUpdater)
	{
		_exchangeRateUpdater = exchangeRateUpdater;
		BtcToUsdExchangeRates = this.WhenAnyValue(provider => provider._exchangeRateUpdater.UsdExchangeRate);

		BtcToUsdExchangeRates.BindTo(this, x => x.UsdExchangeRate);
	}

	public IObservable<decimal> BtcToUsdExchangeRates { get; }

	public Amount Create(Money? money)
	{
		return new Amount(money ?? Money.Zero, this);
	}
}
