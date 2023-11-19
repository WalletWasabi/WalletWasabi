using ReactiveUI;
using System.Reactive.Linq;
using WalletWasabi.Fluent.Models.Currency;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send.CurrencyConversion;

public partial class CurrencyConversionViewModel : ViewModelBase
{
	private bool _isUpdating;
	[AutoNotify] private Amount _amount;
	[AutoNotify] private CurrencyViewModel _left;
	[AutoNotify] private CurrencyViewModel _right;
	[AutoNotify] private bool _isConversionReversed;
	[AutoNotify] private bool _isConversionAvailable;

	public CurrencyConversionViewModel(UiContext uiContext, IWalletModel wallet)
	{
		UiContext = uiContext;
		Wallet = wallet;

		_amount = Amount.Zero;

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

				Left = reversed ? usd : btc;
				Right = reversed ? btc : usd;
			})
			.Subscribe();

		// Bind BTC
		btc.WhenAnyValue(x => x.Value)
		   .Where(_ => !_isUpdating)
		   .Do(btcValue =>
		   {
			   _isUpdating = true;

			   Amount = wallet.AmountProvider.CreateFromBtc(btcValue);
			   usd.Value = Amount.UsdValue;

			   _isUpdating = false;
		   })
		   .Subscribe();

		// Bind USD
		usd.WhenAnyValue(x => x.Value)
		   .Where(_ => !_isUpdating)
		   .Do(usdValue =>
		   {
			   _isUpdating = true;

			   Amount = wallet.AmountProvider.CreateFromUsd(usdValue);
			   btc.Value = Amount.BtcValue;

			   _isUpdating = false;
		   })
		   .Subscribe();

		// Bind Amount
		this.WhenAnyValue(x => x.Amount)
			.Where(_ => !_isUpdating)
			.Do(amnt =>
			{
				_isUpdating = true;

				btc.Value = amnt?.BtcValue;
				usd.Value = amnt?.UsdValue;

				_isUpdating = false;
			});

		IsConversionReversed = UiContext.ApplicationSettings.SendAmountConversionReversed;
	}

	public IWalletModel Wallet { get; }
}
