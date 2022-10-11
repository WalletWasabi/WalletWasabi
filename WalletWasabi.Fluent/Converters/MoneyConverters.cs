using Avalonia.Data.Converters;
using NBitcoin;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.Converters;

public static class MoneyConverters
{
	public static readonly IValueConverter ToFormattedString =
		new FuncValueConverter<Money?, string>(money => money is null ? "" : money.ToFormattedString());

	public static readonly IValueConverter ToUsd =
		new FuncValueConverter<decimal, string>(n => n.RoundToSignificantFigures(5) + " USD");
}
