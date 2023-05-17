using NBitcoin;
using NBitcoin.Payment;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace WalletWasabi.Userfacing;

public static class AddressStringParser
{
	public static bool TryParseBitcoinAddress(string text, Network expectedNetwork, [NotNullWhen(true)] out BitcoinUrlBuilder? url)
	{
		url = null;

		if (text is null || expectedNetwork is null)
		{
			return false;
		}

		text = text.Trim();

		if (text.Length is > 100 or < 20)
		{
			return false;
		}

		try
		{
			var bitcoinAddress = BitcoinAddress.Create(text, expectedNetwork);
			url = new BitcoinUrlBuilder($"bitcoin:{bitcoinAddress}", expectedNetwork);
			return true;
		}
		catch (FormatException)
		{
			return false;
		}
	}

	public static bool TryParseBitcoinUrl(string text, Network expectedNetwork, [NotNullWhen(true)] out BitcoinUrlBuilder? url)
	{
		url = null;

		if (text is null || expectedNetwork is null)
		{
			return false;
		}

		text = text.Trim();

		if (text.Length is > 1000 or < 20)
		{
			return false;
		}

		try
		{
			if (!text.StartsWith("bitcoin:", true, CultureInfo.InvariantCulture))
			{
				return false;
			}

			BitcoinUrlBuilder bitcoinUrl = new(text, expectedNetwork);
			if (bitcoinUrl.Address is { } address && address.Network == expectedNetwork)
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

	public static bool TryParse(string text, Network expectedNetwork, [NotNullWhen(true)] out BitcoinUrlBuilder? result)
	{
		result = null;
		if (string.IsNullOrWhiteSpace(text) || text.Length > 1000)
		{
			return false;
		}

		if (TryParseBitcoinAddress(text, expectedNetwork, out BitcoinUrlBuilder? addressResult))
		{
			result = addressResult;
			return true;
		}
		else
		{
			if (TryParseBitcoinUrl(text, expectedNetwork, out BitcoinUrlBuilder? urlResult))
			{
				result = urlResult;
				return true;
			}
		}
		return false;
	}
}
