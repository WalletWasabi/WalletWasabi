using NBitcoin;
using System.Diagnostics.CodeAnalysis;
using WalletWasabi.Extensions;
using WalletWasabi.Userfacing.Bip21;

namespace WalletWasabi.Userfacing;

public static class AddressStringParser
{
	public static bool TryParse(string text, Network expectedNetwork, [NotNullWhen(true)] out Bip21UriParser.Result? result)
		=> TryParse(text, expectedNetwork, out result, out _);

	/// <summary>
	/// Parses either a Bitcoin address or a BIP21 URI string.
	/// </summary>
	/// <seealso href="https://github.com/lightning/bolts/blob/master/11-payment-encoding.md"/>
	public static bool TryParse(
		string text,
		Network expectedNetwork,
		[NotNullWhen(true)] out Bip21UriParser.Result? result,
		out string? errorMessage)
	{
		result = null;
		errorMessage = null;

		if (text is null || expectedNetwork is null)
		{
			errorMessage = "Internal error.";
			return false;
		}

		text = text.Trim();

		if (text == "")
		{
			errorMessage = "Input length is invalid.";
			return false;
		}

		// Too long URIs/Bitcoin address are unsupported.
		if (text.Length > 1000)
		{
			errorMessage = "Input is too long.";
			return false;
		}

		// Lightning addresses are unsupported.
		bool isLightningAddress = text.StartsWith("lnbc", StringComparison.Ordinal) // Lightning on main.
			|| text.StartsWith("lntb", StringComparison.Ordinal) // Lightning on testnet.
			|| text.StartsWith("lntbs", StringComparison.Ordinal) // Lightning on signet.
			|| text.StartsWith("lnbcrt", StringComparison.Ordinal) // Lightning on regtest
			|| text.StartsWith("lnurl", StringComparison.Ordinal); // Lightning invoice.

		if (isLightningAddress)
		{
			errorMessage = "Lightning addresses are not supported.";
			return false;
		}

		Bip21UriParser.Error? error;

		// Parse a Bitcoin address (not BIP21 URI string)
		if (!text.StartsWith($"{Bip21UriParser.UriScheme}:", StringComparison.OrdinalIgnoreCase))
		{
			if (NBitcoinExtensions.TryParseBitcoinAddressForNetwork(text, expectedNetwork, out BitcoinAddress? address))
			{
				Uri uri = new($"{Bip21UriParser.UriScheme}:{text}");
				result = new Bip21UriParser.Result(uri, expectedNetwork, address);
				return true;
			}

			error = Bip21UriParser.ErrorInvalidAddress with { Details = text };
		}
		else // Parse BIP21 URI string.
		{
			if (Bip21UriParser.TryParse(input: text, expectedNetwork, out result, out error))
			{
				return true;
			}
		}

		errorMessage = error.Message;

		// Special check to verify if the provided Bitcoin address is not for a different Bitcoin network.
		if (error.IsOfSameType(Bip21UriParser.ErrorInvalidAddress))
		{
			Network networkGuess = expectedNetwork == Network.TestNet ? Network.Main : Network.TestNet;

			if (NBitcoinExtensions.TryParseBitcoinAddressForNetwork(error.Details!, networkGuess, out _))
			{
				errorMessage = $"Bitcoin address is valid for {networkGuess} and not for {expectedNetwork}.";
				return false;
			}
		}

		return false;
	}
}
