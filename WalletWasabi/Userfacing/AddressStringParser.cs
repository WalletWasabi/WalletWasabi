using NBitcoin;
using NBitcoin.Payment;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace WalletWasabi.Userfacing;

public static class AddressStringParser
{
	public const string Bip21UriScheme = "bitcoin:";

	public static bool TryParseBitcoinAddress(string text, Network expectedNetwork, [NotNullWhen(true)] out BitcoinUrlBuilder? url)
		=> TryParseBitcoinAddress(text, expectedNetwork, out _, out url);

	public static bool TryParseBitcoinAddress(string text, Network expectedNetwork, [NotNullWhen(false)] out string? errorMessage, [NotNullWhen(true)] out BitcoinUrlBuilder? url)
	{
		url = null;

		if (text is null || expectedNetwork is null)
		{
			errorMessage = "Internal error.";
			return false;
		}

		text = text.Trim();

		if (text.Length is > 100 or < 20)
		{
			errorMessage = "Invalid input length.";
			return false;
		}

		if (!TryGetBitcoinUrlBuilder($"{Bip21UriScheme}{text}", expectedNetwork, out url))
		{
			Network networkGuess = expectedNetwork == Network.TestNet ? Network.Main : Network.TestNet;

			if (!TryGetBitcoinUrlBuilder($"{Bip21UriScheme}{text}", networkGuess, out url))
			{
				errorMessage = "Failed to parse a Bitcoin address.";
			}
			else
			{
				errorMessage = $"Bitcoin address is valid for {networkGuess} and not for {expectedNetwork}.";
			}

			return false;
		}
		else
		{
			errorMessage = null;
			return true;
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

		if (!text.StartsWith(Bip21UriScheme, true, CultureInfo.InvariantCulture))
		{
			errorMessage = $"'{Bip21UriScheme}' prefix is missing.";
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

	public static bool TryParse(string text, Network expectedNetwork, out string? errorMessage, [NotNullWhen(true)] out BitcoinUrlBuilder? result)
	{
		result = null;

		if (string.IsNullOrWhiteSpace(text))
		{
			errorMessage = null;
			return false;
		}

		if (text.Length > 1000)
		{
			errorMessage = "Input is too long.";
			return false;
		}

		if (TryParseBitcoinAddress(text, expectedNetwork, out errorMessage, out BitcoinUrlBuilder? addressResult))
		{
			result = addressResult;
			return true;
		}
		else
		{
			// If the input does not start with "bitcoin:", then we prefer the error message from previous parsing attempt.
			if (text.StartsWith(Bip21UriScheme, StringComparison.Ordinal) && TryParseBitcoinUrl(text, expectedNetwork, out errorMessage, out BitcoinUrlBuilder? builder))
			{
				result = builder;
				return true;
			}
		}

		return false;
	}
}
