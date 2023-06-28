using Avalonia.Data.Converters;
using NBitcoin;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Converters;

public static class MoneyConverters
{
	public static readonly IValueConverter ToUsd =
		new FuncValueConverter<decimal, string>(n => n.ToUsd());

	public static readonly IValueConverter ToUsdNumber =
		new FuncValueConverter<decimal, string>(n => n.ToUsdAmount());

	public static readonly IValueConverter ToUsdAproxBetweenParens =
		new FuncValueConverter<decimal, string>(n => n.ToUsdAproxBetweenParens());

	public static readonly IValueConverter ToBtc =
		new FuncValueConverter<Money, string?>(n => n?.ToBtcWithUnit());

	public static readonly IValueConverter ToFeeWithUnit =
		new FuncValueConverter<Money, string?>(n => n?.ToFeeDisplayUnitFormattedString());

	public static readonly IValueConverter ToFeeWithoutUnit =
		new FuncValueConverter<Money?, string?>(n => n?.ToFeeDisplayUnitRawString());
}
