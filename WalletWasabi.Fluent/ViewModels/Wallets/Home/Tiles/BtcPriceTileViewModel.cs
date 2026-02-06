using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;

public partial class BtcPriceTileViewModel : ActivatableViewModel
{
	[AutoNotify] private decimal _usdPerBtc;

	public BtcPriceTileViewModel(AmountProvider amountProvider)
	{
		amountProvider.BtcToUsdExchangeRate
			.ObserveOn(RxApp.MainThreadScheduler)
			.StartWith(amountProvider.UsdExchangeRate)
			.Subscribe(x => UsdPerBtc = x);
	}
}
