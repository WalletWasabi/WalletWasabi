using NBitcoin;
using ReactiveUI;
using System.Reactive.Linq;
using WalletWasabi.Fluent.Models.Currency;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send.CurrencyConversion;

public partial class CurrencyViewModel : ViewModelBase
{
	[AutoNotify] private decimal _maxValue;
	[AutoNotify] private decimal? _value;
	[AutoNotify] private Amount? _amount;

	public CurrencyViewModel(IWalletModel wallet) : this(wallet, CurrencyFormat.Btc)
	{
	}

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

			// Bind BTC Amount to Value
			this.WhenAnyValue(x => x.Amount)
				.Select(x => x?.BtcValue)
				.BindTo(this, x => x.Value);

			// Bind Value to BTC Amount
			this.WhenAnyValue(x => x.Value)
				.Select(x => x is { } value ? wallet.AmountProvider.Create(new Money(value, MoneyUnit.BTC)) : Amount.Zero)
				.BindTo(this, x => x.Amount);
		}
		else if (format == CurrencyFormat.Usd)
		{
			// Bind USD converted balances to MaxValue
			wallet.Balances
				  .Select(x => x.Usd)
				  .Switch()
				  .BindTo(this, x => x.MaxValue);

			// Bind USD converted Amount to Value
			this.WhenAnyValue(x => x.Amount)
				.Select(x => x is { } a ? a.Usd : Observable.Return(0m))
				.Switch()
				.BindTo(this, x => x.Value);
		}

		Format = format;
	}

	public CurrencyFormat Format { get; }
}
