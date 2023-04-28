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
		=> TryParseBitcoinUrl(text, expectedNetwork, out _, out url);

	public static bool TryParseBitcoinUrl(string text, Network expectedNetwork, [NotNullWhen(false)] out string? errorMessage, [NotNullWhen(true)] out BitcoinUrlBuilder? url)
	{
		errorMessage = null;
		url = null;

		if (text is null || expectedNetwork is null)
		{
			errorMessage = "Internal error.";
			return false;
		}

		text = text.Trim();

		if (text.Length is > 1000 or < 20)
		{
			errorMessage = "Input length is invalid.";
			return false;
		}

		if (!text.StartsWith("bitcoin:", true, CultureInfo.InvariantCulture))
		{
			errorMessage = "'bitcoin:' prefix is missing.";
			return false;
		}

		if (TryGetBitcoinUrlBuilder(text, expectedNetwork, out BitcoinUrlBuilder? builder))
		{
			if (builder.Address is { } address && address.Network == expectedNetwork)
			{
				url = builder;
				return true;
			}

			errorMessage = "Failed to parse a Bitcoin address.";
			return false;
		}

		Network networkGuess = expectedNetwork == Network.TestNet ? Network.Main : Network.TestNet;

		if (TryGetBitcoinUrlBuilder(text, networkGuess, out builder) && builder.Address is { })
		{
			errorMessage = $"Bitcoin address is valid for {networkGuess} and not for {expectedNetwork}.";
			return false;
		}

		errorMessage = "Failed to parse Bitcoin URI.";
		return false;
	}

	private static bool TryGetBitcoinUrlBuilder(string text, Network network, [NotNullWhen(true)] out BitcoinUrlBuilder? builder)
	{
		try
		{
			builder = new(text, network);
			return true;
		}
		catch (FormatException)
		{
			builder = null;
			return false;
		}
	}

	public static bool TryParse(string text, Network expectedNetwork, [NotNullWhen(true)] out BitcoinUrlBuilder? result)
		=> TryParse(text, expectedNetwork, out _, out result);

	public static bool TryParse(string text, Network expectedNetwork, [NotNullWhen(false)] out string? errorMessage, [NotNullWhen(true)] out BitcoinUrlBuilder? result)
	{
		errorMessage = null;
		result = null;

		if (string.IsNullOrWhiteSpace(text) || text.Length > 1000)
		{
			errorMessage = "Input is too long.";
			return false;
		}

		if (TryParseBitcoinAddress(text, expectedNetwork, out BitcoinUrlBuilder? addressResult))
		{
			result = addressResult;
			return true;
		}
		else
		{
			if (TryParseBitcoinUrl(text, expectedNetwork, out errorMessage, out BitcoinUrlBuilder? urlResult))
			{
				result = urlResult;
				return true;
			}
		}

		return false;
	}
}
