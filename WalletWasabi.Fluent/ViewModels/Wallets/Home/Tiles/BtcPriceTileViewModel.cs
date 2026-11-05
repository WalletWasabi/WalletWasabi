using System.Reactive.Linq;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;

public partial class BtcPriceTileViewModel : ActivatableViewModel
{
	[AutoNotify] private decimal _exchangeRate;

	public BtcPriceTileViewModel(UiContext uiContext, AmountProvider amountProvider) : base(uiContext)
	{
		amountProvider.BtcToFiatExchangeRate
			.ObserveOn(RxApp.MainThreadScheduler)
			.StartWith(amountProvider.ExchangeRate)
			.Subscribe(x => ExchangeRate = x);
	}
}
