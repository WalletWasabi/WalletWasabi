using NBitcoin;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public interface IWalletBalancesModel : IBtcAmount
{
	IObservable<Money> Btc { get; }
	IObservable<decimal> Usd { get; }
	IObservable<decimal> ExchangeRate { get; }
	IObservable<bool> HasBalance { get; }
}
