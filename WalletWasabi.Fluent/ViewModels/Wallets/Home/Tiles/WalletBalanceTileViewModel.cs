using System.Reactive.Linq;
using NBitcoin;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;

public class WalletBalanceTileViewModel : ActivatableViewModel
{
	public WalletBalanceTileViewModel(IWalletModel wallet, IObservableExchangeRateProvider exchangeRateProvider)
	{
		var balanceBtc = wallet.Balance;

		BalanceBtc = balanceBtc;
		BalanceFiat = balanceBtc.CombineLatest(exchangeRateProvider.BtcToUsdRate, (btc, exchange) => btc.ToDecimal(MoneyUnit.BTC) * exchange);
		HasBalance = balanceBtc.Select(money => money > Money.Zero);
	}

	public IObservable<bool> HasBalance { get; }

	public IObservable<decimal> BalanceFiat { get; }

	public IObservable<Money> BalanceBtc { get; }
}
