using System.Globalization;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.TransactionBuilding;

namespace WalletWasabi.Fluent.Helpers;

public static class CurrencyUtils
{
	private static NumberFormatInfo FormatInfo = new()
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

	public static string FormattedBtc(this Money amount)
	{
		return amount.ToDecimal(MoneyUnit.BTC).FormattedBtc();
	}

	public static string FormattedBtc(this decimal amount)
	{
		return string.Format(FormatInfo, "{0:### ### ### ##0.#### ####}", amount).Trim();
	}

	public static string FormattedFiat(this decimal amount, string format = "N2")
	{
		return amount.ToString(format, FormatInfo).Trim();
	}
}
