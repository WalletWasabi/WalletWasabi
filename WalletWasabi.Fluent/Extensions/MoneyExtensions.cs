using System.Globalization;
using NBitcoin;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.Extensions;

public static class MoneyExtensions
{
	public static decimal BtcToUsd(this Money money, decimal exchangeRate) => money.ToDecimal(MoneyUnit.BTC) * exchangeRate;
	public static string ToUsdAproxBetweenParens(this decimal n) => $"(≈{ToUsd(n)})";
	public static string ToUsd(this decimal n)
	{
		var amountPart = n switch
		{
			>= 10 => n.RoundToSignificantFigures(5).ToString("N0", FormatInfo),
			>= 1 => n.ToString("N1", FormatInfo),
			_ => n.ToString("N2", FormatInfo)
		};

		return amountPart + " USD";
	}

	private static readonly NumberFormatInfo FormatInfo = new()
	{
		CurrencyGroupSeparator = " ",
		NumberGroupSeparator = " ",
		CurrencyDecimalSeparator = ".",
		NumberDecimalSeparator = "."
	};
}
