using NBitcoin;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;

public class WalletBalanceTileViewModel : ActivatableViewModel
{
	public WalletBalanceTileViewModel(IWalletBalancesModel balances)
	{
		BtcBalance = balances.BtcBalance;
		UsdBalance = balances.UsdBalance;
		HasBalance = balances.HasBalance;
	}

	public IObservable<bool> HasBalance { get; }

	public IObservable<decimal> UsdBalance { get; }

	public IObservable<Money> BtcBalance { get; }
}
