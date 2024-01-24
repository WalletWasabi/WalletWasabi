using System.Reactive.Linq;
using NBitcoin;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Models.Wallets;

public class Amount
{
	public static readonly Amount Zero = new();

	/// <summary>
	/// Private constructor to initialize Zero value
	/// </summary>
	private Amount()
	{
		Btc = Money.Zero;
		Usd = Observable.Return(0m);
	}

	public Amount(Money money, IAmountProvider exchangeRateProvider)
	{
		Btc = money;
		Usd = exchangeRateProvider.BtcToUsdExchangeRates.Select(x => x * Btc.ToDecimal(MoneyUnit.BTC));
	}

	public Money Btc { get; }
	public IObservable<decimal> Usd { get; }
	public bool HasBalance => Btc != Money.Zero;

	public string FormattedBtc => Btc.ToFormattedString();
}
