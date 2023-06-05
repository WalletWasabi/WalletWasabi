namespace WalletWasabi.Fluent.Infrastructure;

public interface IExchangeRateProvider
{
	IObservable<decimal> BtcToUsdRate { get; }
}
