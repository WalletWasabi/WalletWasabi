using System.Reactive.Linq;
using NBitcoin;

namespace WalletWasabi.Fluent.Models.Wallets;

public class Amount
{
	public Amount(Money money, IAmountProvider exchangeRateProvider)
	{
		Btc = money;
		Usd = exchangeRateProvider.BtcToUsdExchangeRates.Select(x => x * Btc.ToDecimal(MoneyUnit.BTC));
	}

	public Money Btc { get; }
	public IObservable<decimal> Usd { get; }
	public bool HasBalance => Btc != Money.Zero;
}
