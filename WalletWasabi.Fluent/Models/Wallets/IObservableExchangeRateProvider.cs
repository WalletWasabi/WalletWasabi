namespace WalletWasabi.Fluent.Models.Wallets;

public interface IObservableExchangeRateProvider
{
	IObservable<decimal> BtcToUsdRate { get; }
}
