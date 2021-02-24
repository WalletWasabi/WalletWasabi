using System.Globalization;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.TransactionBuilding;

namespace WalletWasabi.Fluent.Helpers
{
	public static class CurrencyUtils
	{
		private static NumberFormatInfo s_FormatInfo = new NumberFormatInfo()
		{
			CurrencyGroupSeparator = " ",
			NumberGroupSeparator = " ",
			CurrencyDecimalSeparator = ".",
			NumberDecimalSeparator = "."
		};


		public static string FormattedBtc(this Money amount)
		{
			return amount.ToDecimal(MoneyUnit.BTC).FormattedBtc();
		}

		public static string FormattedBtc(this decimal amount)
		{
			return string.Format(s_FormatInfo, "{0:### ### ### ##0.#### ####}", amount).Trim();
		}

		public static string FormattedFiat(this decimal amount)
		{
			return string.Format(s_FormatInfo, "{0:N2}", amount).Trim();
		}
	}
}