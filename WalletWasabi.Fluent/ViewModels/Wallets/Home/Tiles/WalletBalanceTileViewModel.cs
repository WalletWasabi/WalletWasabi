using System.Reactive.Linq;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;

public class WalletBalanceTileViewModel : ActivatableViewModel
{
	public WalletBalanceTileViewModel(UiContext uiContext, IObservable<Amount> amounts) : base(uiContext)
	{
		Amounts = amounts;

		IsFiatDisplayed = amounts.CombineLatest(
			uiContext.AmountProvider.WhenAnyValue(x => x.ExchangeRate),
			uiContext.ApplicationSettings.WhenAnyValue(x => x.PrivacyMode),
			(amount, exchangeRate, privacyMode) => exchangeRate.Rate != 0 && (amount.HasBalance || privacyMode));
	}

	public IObservable<Amount> Amounts { get; }

	public IObservable<bool> IsFiatDisplayed { get; }
}
