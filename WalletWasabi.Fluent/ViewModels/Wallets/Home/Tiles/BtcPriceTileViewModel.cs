namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;

public class BtcPriceTileViewModel : ActivatableViewModel
{
	public BtcPriceTileViewModel(IWalletBalancesModel balancesModel)
	{
		UsdPerBtc = balancesModel.ExchangeRate;
	}

	public IObservable<decimal> UsdPerBtc { get; }
}
