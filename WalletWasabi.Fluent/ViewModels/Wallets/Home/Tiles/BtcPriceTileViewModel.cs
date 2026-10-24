using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Wallets.Exchange;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;

public partial class BtcPriceTileViewModel : ActivatableViewModel
{
	[AutoNotify] private ExchangeRate _exchangeRate;

	public BtcPriceTileViewModel(UiContext uiContext, AmountProvider amountProvider) : base(uiContext)
	{
		_exchangeRate = amountProvider.ExchangeRate;

		amountProvider.BtcToFiatExchangeRate
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(x => ExchangeRate = x);
	}
}
