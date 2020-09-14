using NBitcoin;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace WalletWasabi.Helpers
{
	public static class BitcoinInput
	{
		private const decimal MinRelayTxFeeRate = 1m;
		private const decimal AbsurdlyHighFeeRate = ((decimal)Constants.MaximumNumberOfSatoshis) / 1000;

		public static bool TryCorrectAmount(in string? original, [NotNullWhen(true)] out string? best)
		{
			var corrected = Guard.Correct(original);
			// Correct amount
			Regex digitsOnly = new Regex(@"[^\d.,٫٬⎖·\']");
			// Make it digits and .,٫٬⎖·\ only.
			corrected = digitsOnly.Replace(corrected, "");

			// https://en.wikipedia.org/wiki/Decimal_separator
			corrected = corrected.Replace(',', '.');
			corrected = corrected.Replace('٫', '.');
			corrected = corrected.Replace('٬', '.');
			corrected = corrected.Replace('⎖', '.');
			corrected = corrected.Replace('·', '.');
			corrected = corrected.Replace('\'', '.');

			// Can trim second ending dot.
			if (corrected.EndsWith('.'))
			{
				corrected = $"{corrected.TrimEnd('.')}.";
			}

			// Trim starting zeros.
			if (corrected.StartsWith("0"))
			{
				// If zeroless starts with a dot, then leave a zero.
				// Else trimm all the zeros.
				var zeroless = corrected.TrimStart('0');
				if (zeroless.Length == 0)
				{
					corrected = "0";
				}
				else if (zeroless.StartsWith('.'))
				{
					corrected = $"0{corrected.TrimStart('0')}";
				}
				else
				{
					corrected = corrected.TrimStart('0');
				}
			}

			// Starting dot means zero. So if the user wants to type .1, then we make it 0.1.
			if (corrected.StartsWith('.'))
			{
				corrected = $".{corrected.TrimStart('.')}";
			}

			if (corrected.Count(x => x == '.') > 1) // Do not enable having two dots.
			{
				// Except if it's at the end. Then we might just remove it.
				corrected = corrected.TrimEnd('.');
				if (corrected.Count(x => x == '.') > 1)
				{
					corrected = "";
				}
			}

			// Enable max 8 decimals.
			var dotIndex = corrected.IndexOf('.');
			if (dotIndex != -1 && corrected.Length - dotIndex > 8)
			{
				corrected = corrected.Substring(0, dotIndex + 1 + 8);
			}

			// Make sure you don't send more bitcoins than how much there is in existence.
			var btc = corrected.Split('.').First();
			if (btc.Length != 0 && (!long.TryParse(btc, out long btcLong) || btcLong >= Constants.MaximumNumberOfWholeBitcoins))
			{
				corrected = Constants.MaximumNumberOfWholeBitcoins.ToString();
			}

			if (corrected != original)
			{
				best = corrected;
				return true;
			}
			else
			{
				best = null;
				return false;
			}
		}

		public static bool TryParseSatoshiFeeText(string feeText, out decimal userFee)
			=> decimal.TryParse(feeText?.Trim(), NumberStyles.AllowDecimalPoint, new CultureInfo("en-US"), out userFee)
			&& userFee >= MinRelayTxFeeRate
			&& userFee < AbsurdlyHighFeeRate
			&& new FeeRate(userFee) is var _;
	}
}
