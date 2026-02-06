using System.Reactive.Disposables;
using System.Reactive.Linq;
using NBitcoin;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Models.Wallets;

/// <summary>
/// Encapsulates a BTC amount and its corresponding USD exchange rate as an Observable sequence.
/// </summary>
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
		HasUsdBalance = Observable.Return(false);
	}

	public Amount(Money money)
	{
		Btc = money;
		Usd = Observable.Return(0m);
		HasUsdBalance = Observable.Return(false);
	}

	public Amount(Money money, AmountProvider exchangeRateProvider)
	{
		Btc = money;
		Usd = exchangeRateProvider.BtcToUsdExchangeRate
			.StartWith(exchangeRateProvider.UsdExchangeRate)
			.Select(x => x * Btc.ToDecimal(MoneyUnit.BTC));
		HasUsdBalance = Usd.Select(x => x != 0m);
	}

	public Money Btc { get; }

	public IObservable<decimal> Usd { get; }

	public bool HasBalance => Btc != Money.Zero;

	public IObservable<bool> HasUsdBalance { get; }

	public string FormattedBtc => Btc.ToFormattedString();
}
