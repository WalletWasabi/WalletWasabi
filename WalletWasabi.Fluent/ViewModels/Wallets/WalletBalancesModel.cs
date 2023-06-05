using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Infrastructure;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public class WalletBalancesModel : ReactiveObject, IWalletBalancesModel
{
	public WalletBalancesModel(IObservable<Money> balances, IExchangeRateProvider exchangeRateProvider)
	{
		ExchangeRate = exchangeRateProvider.BtcToUsdRate;
		Btc = balances;
		Usd = balances.CombineLatest(exchangeRateProvider.BtcToUsdRate, (b, er) => b.ToDecimal(MoneyUnit.BTC) * er);
		HasBalance = balances.Select(money => money != Money.Zero);
	}

	public IObservable<Money> Btc { get; }
	public IObservable<decimal> Usd { get; }
	public IObservable<decimal> ExchangeRate { get; }
	public IObservable<bool> HasBalance { get; }
}
