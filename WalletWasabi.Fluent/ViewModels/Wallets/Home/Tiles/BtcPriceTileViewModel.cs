using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;

public class BtcPriceTileViewModel : ActivatableViewModel
{
	public BtcPriceTileViewModel(IObservableExchangeRateProvider exchangeRateProvider)
	{
		UsdPerBtc = exchangeRateProvider.BtcToUsdRate;
	}

	public IObservable<decimal> UsdPerBtc { get; }
}
