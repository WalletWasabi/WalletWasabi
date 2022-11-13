using Avalonia.Data.Converters;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.Converters;

public static class MoneyConverters
{
	public static readonly IValueConverter ToUsd =
		new FuncValueConverter<decimal, string>(n => n.ToUsd());

	public static readonly IValueConverter ToUsdAproxBetweenParens =
		new FuncValueConverter<decimal, string>(n => n.ToUsdAproxBetweenParens());
}
