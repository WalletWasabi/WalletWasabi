using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Models.Wallets;

public partial class Amount : ReactiveObject
{
	public static readonly Amount Zero = new();

	[AutoNotify(SetterModifier = AccessModifier.Private)] private decimal _btcValue;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private decimal _usdValue;

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

		BtcValue = money.ToDecimal(MoneyUnit.BTC);
		Usd.BindTo(this, x => x.UsdValue);
	}

	public Money Btc { get; }
	public IObservable<decimal> Usd { get; }
	public bool HasBalance => Btc != Money.Zero;

	public string FormattedBtc => Btc.ToFormattedString();
}
