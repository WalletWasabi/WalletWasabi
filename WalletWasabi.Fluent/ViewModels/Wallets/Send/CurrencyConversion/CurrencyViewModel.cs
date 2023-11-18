using ReactiveUI;
using System.Reactive.Linq;
using WalletWasabi.Fluent.Models.Currency;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send.CurrencyConversion;

public partial class CurrencyViewModel : ViewModelBase
{
	[AutoNotify] private decimal _maxValue;
	[AutoNotify] private decimal? _value;

	public CurrencyViewModel(IWalletModel wallet) : this(wallet, CurrencyFormat.Btc)
	{
	}

	public CurrencyViewModel(IWalletModel wallet, CurrencyFormat format)
	{
		// TODO: hardcoded exchange rate selection,
		// this can be improved to have other currencies than BTC and USD.
		if (format == CurrencyFormat.Btc)
		{
			wallet.Balances
				  .Select(x => x.Btc.ToDecimal(NBitcoin.MoneyUnit.BTC))
				  .BindTo(this, x => x.MaxValue);
		}
		else if (format == CurrencyFormat.Usd)
		{
			wallet.Balances
				  .Select(x => x.Usd)
				  .Switch()
				  .BindTo(this, x => x.MaxValue);
		}

		Format = format;
	}

	public CurrencyFormat Format { get; }
}
