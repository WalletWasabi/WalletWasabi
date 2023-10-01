using System.Reactive.Linq;
using NBitcoin;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

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
