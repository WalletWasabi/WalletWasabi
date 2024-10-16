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

	public static readonly IValueConverter ToUsdAmountFormattedWithoutSpaces =
		new FuncValueConverter<decimal, string>(n => n.ToUsdAmountFormatted().Replace(" ", ""));

	public static readonly IValueConverter ToUsdApprox =
		new FuncValueConverter<decimal, string>(n => n.ToUsdAprox());

	public static readonly IValueConverter ToUsdApproxBetweenParens =
		new FuncValueConverter<decimal, string>(n => n.ToUsdAproxBetweenParens());

	private const string BtcAscii = "\u20bf ";

	public static readonly IValueConverter ToBtcRelevantOnly =
		new FuncValueConverter<Money, string?>(n =>
		{

			if (n?.Satoshi == 0)
			{
				return "0";
			}

			var fullString = n?.ToBtcWithUnit();
			if (fullString is null)
			{
				return null;
			}

			var result = fullString;
			foreach (var t in fullString)
			{
				if (t is '+' or '-' or '0' or '.' or ' ')
				{
					result = result.Remove(0, 1);
					continue;
				}

				break;
			}

			if (n?.Satoshi is >= 100000000 or <= -100000000)
			{
				result = BtcAscii + result;
			}
			return result;
		});

	public static readonly IValueConverter ToBtcIrrelevantOnly =
		new FuncValueConverter<Money, string?>(n =>
		{
			switch (n?.Satoshi)
			{
				case 0:
					return BtcAscii;
				case >= 100000000 or <= -100000000:
					return "";
			}

			var fullString = n?.ToBtcWithUnit();
			if (fullString is null)
			{
				return null;
			}

			var result = BtcAscii;
			foreach (var t in fullString)
			{
				switch (t)
				{
					case '0' or '.' or ' ':
						result += t;
						continue;
					case '-' or '+':
						continue;
				}

				break;
			}

			return result;
		});

	public static readonly IValueConverter ToSign =
		new FuncValueConverter<Money, string?>(n =>
		{
			if (n is null)
			{
				return null;
			}

			var result = n.Satoshi switch
			{
				0 => "  ",
				> 0 => "+",
				< 0 => "-"
			};

			return result + " ";
		});

	public static readonly IValueConverter ToFeeWithoutUnit =
		new FuncValueConverter<Money?, string?>(n => n?.ToFeeDisplayUnitRawString());

	public static readonly IValueConverter PercentageDifferenceConverter =
			new FuncValueConverter<double, string>(TextHelpers.FormatPercentageDiff );
}
