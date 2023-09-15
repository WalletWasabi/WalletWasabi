using WalletWasabi.Fluent.Infrastructure;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;

public class BtcPriceTileViewModel : ActivatableViewModel
{
	public BtcPriceTileViewModel(IExchangeRateProvider exchangeRate)
	{
		UsdPerBtc = exchangeRate.BtcToUsdRate;
	}

	public IObservable<decimal> UsdPerBtc { get; }
}
