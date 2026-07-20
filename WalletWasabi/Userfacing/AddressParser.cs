using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Helpers;
using WalletWasabi.Userfacing.Bip321;
using WalletWasabi.Wallets.SilentPayment;
using NBitcoinExtensions = WalletWasabi.Extensions.NBitcoinExtensions;

namespace WalletWasabi.Userfacing;

using AddressParsingResult = Result<Address, string>;

public abstract record Address
{
	public record Bip21Uri(Address Address, decimal? Amount, string? Label, string? PayjoinEndpoint) : Address;
	public record Bitcoin(BitcoinAddress Address) : Address;
	public record SilentPayment(SilentPaymentAddress Address) : Address;

	public string ToWif(Network network) =>
		this switch
		{
			Bitcoin bitcoin => bitcoin.Address.ToString(),
			SilentPayment sp => sp.Address.ToWip(network),
			Bip21Uri bip21 => UriToString(bip21),
			_ => throw new ArgumentException("Unknown address type.")
		};

	private string UriToString(Bip21Uri bip21)
	{
		var parametersArray = new[]
		{
			bip21.Amount is not null ? $"amount={bip21.Amount}" : "",
			bip21.Label is not null ? $"label={bip21.Label}" : "",
			bip21.PayjoinEndpoint is not null ? $"pj={bip21.PayjoinEndpoint}" : ""
		}.Where(x => x != "");
		var parameterString = string.Join("&", parametersArray);

		return string.Join("?", [$"bitcoin:{bip21.Address}", parameterString]);
	}
}

public static class AddressParser
{
	public static AddressParsingResult Parse(string text, Network expectedNetwork)
	{
		text = text.Trim();

		if (text == "")
		{
			return AddressParsingResult.Fail("Input length is invalid.");
		}

		// Too long URIs/Bitcoin address are unsupported.
		if (text.Length > 1000)
		{
			return AddressParsingResult.Fail("Input is too long.");
		}

		// Parse a Bitcoin address (not BIP321 URI string)
		if (!text.StartsWith($"{Bip321UriParser.UriScheme}:", StringComparison.OrdinalIgnoreCase))
		{
			return ParseBitcoinAddress(text, expectedNetwork);
		}

		// Parse BIP321 URI string.
		if (!Bip321UriParser.TryParse(input: text, expectedNetwork, out var result, out var error))
		{
			return AddressParsingResult.Fail(error.Message);
		}

		return AddressParsingResult.Ok(
			new Address.Bip21Uri(
				result.Address,
				result.Amount is null ? null : decimal.Parse(result.Amount.ToString()),
				result.Label,
				result.UnknownParameters.GetValueOrDefault("pj")));
	}

	public static AddressParsingResult ParseBitcoinAddress(string text, Network expectedNetwork)
	{
		if (NBitcoinExtensions.TryParseBitcoinAddressForNetwork(text, expectedNetwork, out BitcoinAddress? address))
		{
			return AddressParsingResult.Ok(new Address.Bitcoin(address));
		}

		return ParseSilentPaymentAddress(text, expectedNetwork);
	}

	public static AddressParsingResult ParseSilentPaymentAddress(string text, Network expectedNetwork)
	{
		try
		{
			var silentPaymentAddress = SilentPaymentAddress.Parse(text, expectedNetwork);
			return AddressParsingResult.Ok(new Address.SilentPayment(silentPaymentAddress));
		}
		catch
		{
			return AddressParsingResult.Fail(Bip321UriParser.ErrorInvalidAddress.Message);
		}
	}
}
