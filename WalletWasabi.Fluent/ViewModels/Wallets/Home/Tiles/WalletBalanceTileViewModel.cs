using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;

public class WalletBalanceTileViewModel : ActivatableViewModel
{
	public WalletBalanceTileViewModel(IObservable<Amount> amounts)
	{
		Amounts = amounts;
	}

	public IObservable<Amount> Amounts { get; }
}
