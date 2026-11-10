using System.Reactive.Linq;
using NBitcoin;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Models.Wallets;

/// <summary>
/// Encapsulates a BTC amount and its corresponding fiat amount as an Observable sequence.
/// </summary>
public class Amount
{
	public static readonly Amount Zero = new(Money.Zero);

	public Amount(Money money)
	{
		Btc = money;
		Fiat = Observable.Empty<FiatAmount>();
		HasFiatBalance = Observable.Return(false);
	}

	public Amount(Money money, AmountProvider exchangeRateProvider)
	{
		Btc = money;
		Fiat = exchangeRateProvider.BtcToFiatExchangeRate
			.StartWith(exchangeRateProvider.ExchangeRate)
			.Select(x => Btc.BtcToFiat(x));
		HasFiatBalance = Fiat.Select(x => x.Value != 0m);
	}

	public Money Btc { get; }

	public IObservable<FiatAmount> Fiat { get; }

	public bool HasBalance => Btc != Money.Zero;

	public IObservable<bool> HasFiatBalance { get; }

	public string FormattedBtc => Btc.ToFormattedString();
}
