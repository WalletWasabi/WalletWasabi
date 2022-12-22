using System.Reactive.Linq;
using NBitcoin;

namespace WalletWasabi.Fluent.Helpers;

public class BalanceHelper
{
	public BalanceHelper(IObservable<decimal> exchangeRate, IObservable<Money> balance)
	{
		ExchangeRate = exchangeRate;
		Balance = balance;
		UsdBalance = balance.CombineLatest(exchangeRate, (b, er) => b.ToDecimal(MoneyUnit.BTC) * er);
	}

	public IObservable<Money> Balance { get; }
	public IObservable<decimal> UsdBalance { get; }
	public IObservable<decimal> ExchangeRate { get; }
}
