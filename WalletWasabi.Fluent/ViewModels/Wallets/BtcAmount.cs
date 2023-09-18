using System.Reactive.Linq;
using NBitcoin;
using WalletWasabi.Fluent.Infrastructure;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public class BtcAmount
{
	private BtcAmount(Money value, IExchangeRateProvider exchangeRateProvider)
	{
		Value = value;
		UsdValue = exchangeRateProvider.BtcToUsdRate.Select(x => x * Value.ToDecimal(MoneyUnit.BTC));
		ExchangeRates = exchangeRateProvider.BtcToUsdRate;
	}

	public Money Value { get; }
	public IObservable<decimal> UsdValue { get; }
	public IObservable<decimal> ExchangeRates { get; }

	public static BtcAmount Create(Money value)
	{
		return new BtcAmount(value, new ExchangeRateProvider(Services.Synchronizer));
	}
}
