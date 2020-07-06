using NBitcoin;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WalletWasabi.Helpers
{
	public static class StringFormatHelpers
	{
		private static NumberFormatInfo CurrencyNumberFormat = new NumberFormatInfo()
		{
			NumberGroupSeparator = " ",
			NumberDecimalDigits = 0
		};

		private static string ToCurrency(this Money btc, string currency, decimal exchangeRate, bool lurkingWifeMode = false)
		{
			var dollars = exchangeRate * btc.ToDecimal(MoneyUnit.BTC);

			return lurkingWifeMode
				? $"### {currency}"
				: exchangeRate == default
					? $"??? {currency}"
					: $"{dollars.ToString("N", CurrencyNumberFormat)} {currency}";
		}

		public static string ToUsd(this Money btc, decimal usdExchangeRate, bool lurkingWifeMode = false)
		{
			return ToCurrency(btc, "USD", usdExchangeRate, lurkingWifeMode);
		}
	}
}
