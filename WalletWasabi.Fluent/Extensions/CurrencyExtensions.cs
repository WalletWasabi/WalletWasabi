using System.Globalization;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Extensions;

public static class CurrencyExtensions
{
	private static readonly NumberFormatInfo FormatInfo = new()
	{
		CurrencyGroupSeparator = " ",
		NumberGroupSeparator = " ",
		CurrencyDecimalSeparator = ".",
		NumberDecimalSeparator = "."
	};

	public static Money CalculateDestinationAmount(this BuildTransactionResult result)
	{
		var isNormalPayment = result.OuterWalletOutputs.Any();

		if (isNormalPayment)
		{
			return result.OuterWalletOutputs.Sum(x => x.Amount);
		}
		else
		{
			return result.InnerWalletOutputs
				.Where(x => !x.HdPubKey.IsInternal)
				.Select(x => x.Amount)
				.Sum();
		}
	}

	public static string FormattedBtc(this decimal amount)
	{
		return string.Format(FormatInfo, "{0:### ### ### ##0.#### ####}", amount).Trim();
	}

	public static string FormattedFiat(this decimal amount, string format = "N2")
	{
		return amount.ToString(format, FormatInfo).Trim();
	}

	public static decimal BtcToUsd(this Money money, decimal exchangeRate) => money.ToDecimal(MoneyUnit.BTC) * exchangeRate;

	public static string ToUsdAproxBetweenParens(this decimal n) => $"(≈{ToUsd(n)})";

	public static string ToUsd(this decimal n)
	{
		return ToUsdAmount(n) + " USD";
	}

	public static string ToUsdAmount(this decimal n)
	{
		return n switch
		{
			>= 10 => Math.Ceiling(n).ToString("N0", FormatInfo),
			>= 1 => n.ToString("N1", FormatInfo),
			_ => n.ToString("N2", FormatInfo)
		};
	}

	public static string ToBtcWithUnitAndConversion(this Money money, decimal exchangeRate) => money.ToBtcWithUnit() + " " + (money.ToDecimal(MoneyUnit.BTC) * exchangeRate).ToUsdAproxBetweenParens();

	public static string ToFeeWithConversion(this Money money, decimal exchangeRate) => money.ToFeeDisplayUnitFormattedString() + " " + (money.ToDecimal(MoneyUnit.BTC) * exchangeRate).ToUsdAproxBetweenParens();
}
