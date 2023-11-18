using NBitcoin;
using ReactiveUI;
using System.Reactive.Linq;
using WalletWasabi.Fluent.Models.Currency;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send.CurrencyConversion;

public partial class CurrencyConversionViewModel : ViewModelBase
{
	[AutoNotify] private Money? _amount;
	[AutoNotify] private CurrencyViewModel _left;
	[AutoNotify] private CurrencyViewModel _right;
	[AutoNotify] private bool _isConversionReversed;
	[AutoNotify] private bool _isConversionAvailable;

	public CurrencyConversionViewModel(UiContext uiContext, IWalletModel wallet)
	{
		UiContext = uiContext;
		Wallet = wallet;

		// TODO: this could be used to show conversion to currencies other than USD
		var btc = new CurrencyViewModel(wallet, CurrencyFormat.Btc);
		var usd = new CurrencyViewModel(wallet, CurrencyFormat.Usd);

		_left = btc;
		_right = usd;

		this.WhenAnyValue(x => x.Wallet.AmountProvider.UsdExchangeRate)
			.Do(x => IsConversionAvailable = x > 0)
			.Subscribe();

		this.WhenAnyValue(x => x.IsConversionReversed)
			.Do(reversed =>
			{
				UiContext.ApplicationSettings.SendAmountConversionReversed = reversed;

				if (reversed)
				{
					Left = usd;
					Right = btc;
				}
				else
				{
					Left = btc;
					Right = usd;
				}
			})
			.Subscribe();

		btc.WhenAnyValue(x => x.Amount)
		   .BindTo(usd, x => x.Amount);

		usd.WhenAnyValue(x => x.Amount)
		   .BindTo(btc, x => x.Amount);

		IsConversionReversed = UiContext.ApplicationSettings.SendAmountConversionReversed;
	}

	public IWalletModel Wallet { get; }
}
