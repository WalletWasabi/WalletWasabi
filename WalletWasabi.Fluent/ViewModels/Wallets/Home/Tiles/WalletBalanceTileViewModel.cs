using System.Reactive.Linq;
using NBitcoin;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;

public class WalletBalanceTileViewModel : ActivatableViewModel
{
	private readonly IObservable<Amount> _balances;

	public WalletBalanceTileViewModel(IObservable<Amount> balances)
	{
		_balances = balances;
	}

	public IObservable<bool> HasBalance => _balances.Select(amount => amount.Btc != Money.Zero);

	public IObservable<decimal> UsdBalance => _balances.Select(x => x.Usd).Switch();

	public IObservable<Money> BtcBalance => _balances.Select(x => x.Btc);
}
