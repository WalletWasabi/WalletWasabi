using Avalonia.Data.Converters;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.Converters;

public static class ExchangeRateConverters
{
	public static readonly IValueConverter ToUsdBtcExchangeRate =
		new FuncValueConverter<decimal, string>(n => n == 0 ? "N/A" : n.ToFiatFormatted());
}
