using System.Reactive.Linq;
using NBitcoin;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public class WalletBalancesModel : IWalletBalancesModel
{
	public WalletBalancesModel(IWalletModel wallet, IObservableExchangeRateProvider exchangeRateProvider)
	{
		ExchangeRate = exchangeRateProvider.BtcToUsdRate;
		BtcBalance = wallet.Balance;
		UsdBalance = wallet.Balance.CombineLatest(exchangeRateProvider.BtcToUsdRate, (b, er) => b.ToDecimal(MoneyUnit.BTC) * er);
		HasBalance = wallet.Balance.Select(money => money != Money.Zero);
	}

	public IObservable<Money> BtcBalance { get; }
	public IObservable<decimal> UsdBalance { get; }
	public IObservable<decimal> ExchangeRate { get; }
	public IObservable<bool> HasBalance { get; }
}
