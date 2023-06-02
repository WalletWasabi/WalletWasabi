namespace WalletWasabi.Fluent.Infrastructure;

public interface IObservableExchangeRateProvider
{
	IObservable<decimal> BtcToUsdRate { get; }
}
