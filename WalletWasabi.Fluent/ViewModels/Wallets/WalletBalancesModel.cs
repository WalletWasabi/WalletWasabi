using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Infrastructure;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public class WalletBalancesModel : ReactiveObject, IWalletBalancesModel
{
	public WalletBalancesModel(IObservable<BtcAmount> balances)
	{
		ExchangeRate = balances.Select(x => x.ExchangeRate).Switch();
		Btc = balances.Select(x => x.Value);
		Usd = balances.Select(x => x.UsdValue).Switch();
		HasBalance = balances.Select(money => money.Value != Money.Zero);
	}

	public IObservable<Money> Btc { get; }
	public IObservable<decimal> Usd { get; }
	public IObservable<decimal> ExchangeRate { get; }
	public IObservable<bool> HasBalance { get; }
}

public class BtcAmount
{
	public BtcAmount(Money value, IExchangeRateProvider exchangeRateProvider)
	{
		Value = value;
		UsdValue = exchangeRateProvider.BtcToUsdRate.Select(x => x * Value.ToDecimal(MoneyUnit.BTC));
		ExchangeRate = exchangeRateProvider.BtcToUsdRate;
	}

	public Money Value { get; }
	public IObservable<decimal> UsdValue { get; }
	public IObservable<decimal> ExchangeRate { get; }
}
