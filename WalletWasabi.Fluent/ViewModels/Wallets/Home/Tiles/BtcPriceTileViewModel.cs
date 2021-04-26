using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles
{
	public partial class BtcPriceTileViewModel : TileViewModel
	{
		[AutoNotify] private string _btcPrice;

		private readonly Wallet _wallet;

		public BtcPriceTileViewModel(Wallet wallet)
		{
			_wallet = wallet;

			_wallet.Synchronizer.WhenAnyValue(x => x.UsdExchangeRate)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(usd => BtcPrice = usd.FormattedFiat());
				//.DisposeWith(Disposables);
		}
	}
}