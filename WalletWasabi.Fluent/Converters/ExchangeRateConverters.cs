using Avalonia.Data.Converters;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Wallets.Exchange;

namespace WalletWasabi.Fluent.Converters;

public static class ExchangeRateConverters
{
	public static readonly IValueConverter ToFiatPerBtc =
		new FuncValueConverter<ExchangeRate?, string?>(r => r switch
		{
			null => null,
			{ Rate: 0m } => "N/A",
			_ => new FiatAmount(r.Rate, r.Ticker).ToFiatFormatted()
		});
}
