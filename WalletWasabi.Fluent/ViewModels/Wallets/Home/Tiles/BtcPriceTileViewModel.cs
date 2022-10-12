using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;

public class BtcPriceTileViewModel : TileViewModel
{
	public BtcPriceTileViewModel(Wallet wallet)
	{
		Wallet = wallet;

		UsdPerBtc = this
			.WhenAnyValue(x => x.Wallet.Synchronizer.UsdExchangeRate)
			.ObserveOn(RxApp.MainThreadScheduler)
			.ReplayLastActive();
	}

	public IObservable<decimal> UsdPerBtc { get; }

	private Wallet Wallet { get; }
}
