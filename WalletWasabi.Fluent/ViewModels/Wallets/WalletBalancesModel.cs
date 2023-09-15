using System.Reactive.Linq;
using System.Reactive.Subjects;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Infrastructure;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public class WalletBalancesModel : ReactiveObject, IWalletBalancesModel
{
	private readonly IObservable<BtcAmount> _balances;
	private readonly BehaviorSubject<Money> _val;

	public WalletBalancesModel(IObservable<BtcAmount> balances)
	{
		_balances = balances;
		ExchangeRate = balances.Select(x => x.ExchangeRates).Switch();
		Btc = balances.Select(x => x.Value);
		Usd = balances.Select(x => x.UsdValue).Switch();
		HasBalance = balances.Select(money => money.Value != Money.Zero);
		
		_val = new BehaviorSubject<Money>(Money.Zero);
		Btc.Subscribe(_val);
	}

	public IObservable<Money> Btc { get; }
	public IObservable<decimal> Usd { get; }
	public IObservable<decimal> ExchangeRate { get; }
	public IObservable<bool> HasBalance { get; }
	public Money Value => _val.Value;
	public IObservable<decimal> UsdValue => _balances.Select(x => x.UsdValue).Switch();
	public IObservable<decimal> ExchangeRates => _balances.Select(amount => amount.ExchangeRates).Switch();
}

public class BtcAmount : IBtcAmount
{
	public BtcAmount(Money value, IExchangeRateProvider exchangeRateProvider)
	{
		Value = value;
		UsdValue = exchangeRateProvider.BtcToUsdRate.Select(x => x * Value.ToDecimal(MoneyUnit.BTC));
		ExchangeRates = exchangeRateProvider.BtcToUsdRate;
	}

	public Money Value { get; }
	public IObservable<decimal> UsdValue { get; }
	public IObservable<decimal> ExchangeRates { get; }
}

public interface IBtcAmount
{
	Money Value { get; }
	IObservable<decimal> UsdValue { get; }
	IObservable<decimal> ExchangeRates { get; }
}
