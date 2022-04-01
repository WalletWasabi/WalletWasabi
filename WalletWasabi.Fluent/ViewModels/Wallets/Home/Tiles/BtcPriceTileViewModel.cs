using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;

public partial class BtcPriceTileViewModel : TileViewModel
{
	private readonly Wallet _wallet;
	[AutoNotify] private string _btcPrice;

	public BtcPriceTileViewModel(Wallet wallet)
	{
		_wallet = wallet;
		_btcPrice = "";
	}

	protected override void OnActivated(CompositeDisposable disposables)
	{
		base.OnActivated(disposables);

		_wallet.Synchronizer.WhenAnyValue(x => x.UsdExchangeRate)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(usd => BtcPrice = usd.FormattedFiat(format: "N0") + " USD")
			.DisposeWith(disposables);
	}
}
