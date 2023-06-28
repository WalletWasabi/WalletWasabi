using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Infrastructure;

public class DualAmount : ReactiveObject
{
	private readonly ObservableAsPropertyHelper<decimal> _usd;
	public Money Btc { get; }

	public DualAmount(Money btc, IExchangeRateProvider exchangeRateProvider)
	{
		Btc = btc;
		_usd = exchangeRateProvider.BtcToUsdRate.Select(r => r * btc.ToDecimal(MoneyUnit.BTC)).ToProperty(this, x => x.Usd);
	}

	public decimal Usd => _usd.Value;

	public string AmountWithConversion => Btc.ToBtcWithUnit() + " " + Usd.ToUsdAproxBetweenParens();
	public string FeeWithConversion => Btc.ToFeeDisplayUnitFormattedString() + " " + Usd.ToUsdAproxBetweenParens();
	public string FeeWithoutUnit => Btc.ToFeeDisplayUnitRawString();
}
