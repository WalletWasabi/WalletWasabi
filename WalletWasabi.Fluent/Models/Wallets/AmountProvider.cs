using NBitcoin;
using ReactiveUI;
using WalletWasabi.Wallets.Exchange;

namespace WalletWasabi.Fluent.Models.Wallets;

[AutoInterface]
public partial class AmountProvider : ReactiveObject
{
	private readonly ExchangeRateUpdater _exchangeRateUpdater;
	[AutoNotify] private decimal _usdExchangeRate;

	public AmountProvider()
	{
		_exchangeRateUpdater = Services.HostedServices.Get<ExchangeRateUpdater>();
		BtcToUsdExchangeRate = this.WhenAnyValue(provider => provider._exchangeRateUpdater.UsdExchangeRate);

		BtcToUsdExchangeRate.BindTo(this, x => x.UsdExchangeRate);
	}

	public IObservable<decimal> BtcToUsdExchangeRate { get; }

	public Amount Create(Money? money)
	{
		return new Amount(money ?? Money.Zero, this);
	}
}
