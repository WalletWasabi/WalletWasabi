using ReactiveUI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Services;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public class ObservableExchangeRateProvider : IObservableExchangeRateProvider
{
	public WasabiSynchronizer Synchronizer { get; }

	public ObservableExchangeRateProvider(WasabiSynchronizer synchronizer)
	{
		Synchronizer = synchronizer;

		BtcToUsdRate = this.WhenAnyValue(x => x.Synchronizer.UsdExchangeRate);
	}

	public IObservable<decimal> BtcToUsdRate { get; }
}
