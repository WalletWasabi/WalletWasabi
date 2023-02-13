using System.Reactive.Linq;
using NBitcoin;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public class WalletBalances
{
	public WalletBalances(IObservable<decimal> exchangeRate, IObservable<Money> btcBalance)
	{
		ExchangeRate = exchangeRate;
		BtcBalance = btcBalance;
		UsdBalance = btcBalance.CombineLatest(exchangeRate, (b, er) => b.ToDecimal(MoneyUnit.BTC) * er);
	}

	public IObservable<Money> BtcBalance { get; }
	public IObservable<decimal> UsdBalance { get; }
	public IObservable<decimal> ExchangeRate { get; }
}
