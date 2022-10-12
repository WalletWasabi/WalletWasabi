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
		var amountPart = n < 10 ? n.ToString("N2", FormatInfo) : n.RoundToSignificantFigures(5).ToString("N0", FormatInfo);
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
