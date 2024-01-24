using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Models.Wallets;

public partial class Amount : ReactiveObject
{
	public static readonly Amount Zero = new(Money.Zero, 0m);
	public static readonly Amount Invalid = new(null!, -1m);

	[AutoNotify(SetterModifier = AccessModifier.Private)] private decimal _btcValue;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private decimal _usdValue;

	/// <summary>
	/// Private constructor to initialize Zero and Invalid values
	/// </summary>
	private Amount(Money btc, decimal usd)
	{
		Btc = btc;
		Usd = Observable.Return(usd);
	}

	public Amount(Money money, IAmountProvider exchangeRateProvider)
	{
		Btc = money;
		Usd = exchangeRateProvider.BtcToUsdExchangeRates.Select(x => x * Btc.ToDecimal(MoneyUnit.BTC));

		BtcValue = money.ToDecimal(MoneyUnit.BTC);
		Usd.BindTo(this, x => x.UsdValue);
	}

	public Money Btc { get; }
	public IObservable<decimal> Usd { get; }
	public bool HasBalance => Btc != Money.Zero;

	public string FormattedBtc => Btc.ToFormattedString();

	public static bool operator ==(Amount? x, Amount? y)
	{
		if (x is null)
		{
			return y is null;
		}

		return x.Equals(y);
	}

	public static bool operator !=(Amount? x, Amount? y) => !(x == y);

	public override bool Equals(object? obj)
	{
		if (ReferenceEquals(this, obj))
		{
			return true;
		}

		if (obj is not Amount amount)
		{
			return false;
		}

		return amount.Btc == Btc;
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(Btc);
	}
}
