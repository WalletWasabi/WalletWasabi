using System.Reactive.Linq;
using NBitcoin;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;

public class WalletBalanceTileViewModel : ActivatableViewModel
{
	private readonly IObservable<BtcAmount> _balances;

	public WalletBalanceTileViewModel(IObservable<BtcAmount> balances)
	{
		_balances = balances;
	}

	public IObservable<bool> HasBalance => _balances.Select(amount => amount.Value != Money.Zero);

	public IObservable<decimal> UsdBalance => _balances.Select(x => x.UsdValue).Switch();

	public IObservable<Money> BtcBalance => _balances.Select(x => x.Value);
}
