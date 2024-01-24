using NBitcoin;
using ReactiveUI;
using System.Reactive.Linq;
using WalletWasabi.Fluent.Models.Currency;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send.CurrencyConversion;

public partial class CurrencyViewModel : ViewModelBase
{
	[AutoNotify] private decimal _maxValue;
	[AutoNotify] private decimal _minSuggestionValue;
	[AutoNotify] private CurrencyValue _value = CurrencyValue.EmptyValue;

	private bool _isUpdating;

	public CurrencyViewModel(IWalletModel wallet, CurrencyFormat format)
	{
		// TODO: hardcoded exchange rate selection,
		// this can be improved to have other currencies than BTC and USD.
		if (format == CurrencyFormat.Btc)
		{
			// Bind BTC balance to MaxValue
			wallet.Balances
				  .Select(x => x.Btc.ToDecimal(MoneyUnit.BTC))
				  .BindTo(this, x => x.MaxValue);
		}
		else if (format == CurrencyFormat.Usd)
		{
			// Bind USD converted balances to MaxValue
			wallet.Balances
				  .Select(x => x.Usd)
				  .Switch()
				  .BindTo(this, x => x.MaxValue);

			MinSuggestionValue = 1;
		}

		Format = format;

		this.WhenAnyValue(x => x.Format.Value)
			.Where(_ => !_isUpdating)
			.Do(v =>
			{
				_isUpdating = true;
				Value = Format.Value = v;
				_isUpdating = false;
			})
			.Subscribe();

		this.WhenAnyValue(x => x.Value)
			.Where(_ => !_isUpdating)
			.Do(x =>
			{
				_isUpdating = true;
				Format.SetValue(x);
				_isUpdating = false;
			})
			.Subscribe();
	}

	public CurrencyFormat Format { get; }
}
