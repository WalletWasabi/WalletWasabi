using NBitcoin;
using NBitcoin.Payment;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WalletWasabi.Helpers
{
	public static class AddressStringParser
	{
		public static bool TryParseBitcoinAddress(string text, Network expectedNetwork, out BitcoinUrlBuilder url)
		{
			url = null;

			if (text is null || expectedNetwork is null)
			{
				return false;
			}

			text = text.Trim();

			if (text.Length > 100 || text.Length < 20)
			{
				return false;
			}

			try
			{
				var bitcoinAddress = BitcoinAddress.Create(text, expectedNetwork);
				url = new BitcoinUrlBuilder($"bitcoin:{bitcoinAddress}");
				return true;
			}
			catch (FormatException)
			{
				return false;
			}
		}

		public static bool TryParseBitcoinUrl(string text, Network expectedNetwork, out BitcoinUrlBuilder url)
		{
			url = null;

			if (text is null || expectedNetwork is null)
			{
				return false;
			}

			text = text.Trim();

			if (text.Length > 1000 || text.Length < 20)
			{
				return false;
			}

			try
			{
				if (!text.StartsWith("bitcoin:", true, CultureInfo.InvariantCulture))
				{
					return false;
				}

				var bitcoinUrl = new BitcoinUrlBuilder(text);
				if (bitcoinUrl?.Address.Network == expectedNetwork)
				{
					url = bitcoinUrl;
					return true;
				}

				return false;
			}
			catch (FormatException)
			{
				return false;
			}
		}

		public static bool TryParse(string text, Network expectedNetwork, out BitcoinUrlBuilder result)
		{
			result = null;
			if (string.IsNullOrWhiteSpace(text) || text.Length > 1000)
			{
				return false;
			}

			if (TryParseBitcoinAddress(text, expectedNetwork, out BitcoinUrlBuilder addressResult))
			{
				result = addressResult;
				return true;
			}
			else
			{
				if (TryParseBitcoinUrl(text, expectedNetwork, out BitcoinUrlBuilder urlResult))
				{
					result = urlResult;
					return true;
				}
			}
			return false;
		}
	}
}
