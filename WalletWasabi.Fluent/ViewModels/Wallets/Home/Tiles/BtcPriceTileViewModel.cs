using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;

public partial class BtcPriceTileViewModel : ActivatableViewModel
{
	[AutoNotify] private decimal _usdPerBtc;

	public BtcPriceTileViewModel(UiContext uiContext, AmountProvider amountProvider) : base(uiContext)
	{
		amountProvider.BtcToUsdExchangeRate
			.ObserveOn(RxSchedulers.MainThreadScheduler)
			.StartWith(amountProvider.UsdExchangeRate)
			.Subscribe(x => UsdPerBtc = x);
	}
}
