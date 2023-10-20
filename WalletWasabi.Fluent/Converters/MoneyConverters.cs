using System.Globalization;
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
			new FuncValueConverter<double, string>(n =>
			{
				var precision = 0.01m;
				var withFriendlyDecimals = n.WithFriendlyDecimals();

				string diffPart;
				if (Math.Abs(withFriendlyDecimals) < precision)
				{
					var threshold = withFriendlyDecimals > 0 ? precision : -precision;
					diffPart = "less than " + threshold.ToString(CultureInfo.InvariantCulture);
				}
				else
				{
					diffPart = withFriendlyDecimals.ToString(CultureInfo.InvariantCulture);
				}

				var numericPart = n > 0 ? "+" + diffPart : diffPart;
				return numericPart + "%";
			});
}
