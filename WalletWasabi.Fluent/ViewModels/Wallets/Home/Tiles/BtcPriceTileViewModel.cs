using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;

public class BtcPriceTileViewModel : ActivatableViewModel
{
	public BtcPriceTileViewModel(Wallet wallet)
	{
		Wallet = wallet;

		UsdPerBtc = this
			.WhenAnyValue(x => x.Wallet.Synchronizer.UsdExchangeRate)
			.ObserveOn(RxApp.MainThreadScheduler);
	}

	public IObservable<decimal> UsdPerBtc { get; }

	private Wallet Wallet { get; }
}
