using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;

public class BtcPriceTileViewModel : ActivatableViewModel
{
	public BtcPriceTileViewModel(IAmountProvider amountProvider)
	{
		UsdPerBtc = amountProvider.BtcToUsdExchangeRate;
	}

	public IObservable<decimal> UsdPerBtc { get; }
}
