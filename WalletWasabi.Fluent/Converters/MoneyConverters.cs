using System.Globalization;
using Avalonia.Data.Converters;
using NBitcoin;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Converters;

public static class MoneyConverters
{
	public static readonly IValueConverter ToUsdFormatted =
		new FuncValueConverter<decimal, string>(n => n.ToUsdFormatted());

	public static readonly IValueConverter ToUsdNumber =
		new FuncValueConverter<Money, string?>(n => n?.ToDecimal(MoneyUnit.BTC).WithFriendlyDecimals().ToString(CultureInfo.InvariantCulture));

	public static readonly IValueConverter ToUsdFriendlyDecimals =
		new FuncValueConverter<decimal, string>(n => n.WithFriendlyDecimals().ToString(CultureInfo.InvariantCulture));

	public static readonly IValueConverter ToUsdApprox =
		new FuncValueConverter<decimal, string>(n => n.ToUsdAprox());

	public static readonly IValueConverter ToUsdApproxBetweenParens =
		new FuncValueConverter<decimal, string>(n => n.ToUsdAproxBetweenParens());

	public static readonly IValueConverter ToBtc =
		new FuncValueConverter<Money, string?>(n => n?.ToBtcWithUnit());

	public static readonly IValueConverter ToFeeWithUnit =
		new FuncValueConverter<Money, string?>(n => n?.ToFeeDisplayUnitFormattedString());

	public static readonly IValueConverter ToFeeWithoutUnit =
		new FuncValueConverter<Money?, string?>(n => n?.ToFeeDisplayUnitRawString());

	public static readonly IValueConverter PercentageDifferenceConverter =
			new FuncValueConverter<double, string>(TextHelpers.FormatPercentageDiff );
}
