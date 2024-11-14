using System.Collections.Generic;
using NBitcoin;
using WalletWasabi.Helpers;
using WalletWasabi.Userfacing.Bip21;
using WalletWasabi.Wallets.SilentPayment;
using NBitcoinExtensions = WalletWasabi.Extensions.NBitcoinExtensions;

namespace WalletWasabi.Userfacing;

using AddressStringParserResult = Result<AddressStringParserSuccess, AddressStringParserError>;

public abstract record AddressStringParserSuccess
{
	private AddressStringParserSuccess(string displayAddress)
	{
		DisplayAddress = displayAddress;
	}

	public record Bip21UriParsed(string DisplayAddress, BitcoinAddress Address, string Amount, string Label, string? PayjoinEndpoint) : AddressStringParserSuccess(DisplayAddress);
	public record BitcoinAddressParsed(string DisplayAddress, BitcoinAddress Address) : AddressStringParserSuccess(DisplayAddress);
	public record SilentPaymentAddressParsed(string DisplayAddress, SilentPaymentAddress Address) : AddressStringParserSuccess(DisplayAddress);

	public string DisplayAddress { get; }
}

public abstract record AddressStringParserError
{
	private AddressStringParserError(string message)
	{
		Message = message;
	}

	public record InputLengthInvalid(string Message) : AddressStringParserError(Message);
	public record InputTooLong(string Message) : AddressStringParserError(Message);
	public record LightningUnsupported(string Message) : AddressStringParserError(Message);
	public record WrongNetwork(string Message) : AddressStringParserError(Message);
	public record GenericError(string Message) : AddressStringParserError(Message);

	public string Message { get; }
}

public static class AddressStringParser
{
	/// <summary>
	/// Parses either a Bitcoin address, a BIP21 URI string, a Silent Payment address or a Payjoin endpoint.
	/// </summary>
	/// <seealso href="https://github.com/lightning/bolts/blob/master/11-payment-encoding.md"/>
	public static AddressStringParserResult TryParse(string text, Network expectedNetwork)
	{
		text = text.Trim();

		if (text == "")
		{
			return AddressStringParserResult.Fail(new AddressStringParserError.InputLengthInvalid("Input length invalid."));
		}

		// Too long URIs/Bitcoin address are unsupported.
		if (text.Length > 1000)
		{
			return AddressStringParserResult.Fail(new AddressStringParserError.InputTooLong("Input is too long."));

		}

		// Lightning addresses are unsupported.
		bool isLightningAddress = text.StartsWith("lnbc", StringComparison.Ordinal) // Lightning on main.
			|| text.StartsWith("lntb", StringComparison.Ordinal) // Lightning on testnet.
			|| text.StartsWith("lntbs", StringComparison.Ordinal) // Lightning on signet.
			|| text.StartsWith("lnbcrt", StringComparison.Ordinal) // Lightning on regtest
			|| text.StartsWith("lnurl", StringComparison.Ordinal); // Lightning invoice.

		if (isLightningAddress)
		{
			return AddressStringParserResult.Fail(new AddressStringParserError.LightningUnsupported("Lightning addresses are not supported."));

		}

		Bip21UriParser.Error? error;

		// Parse a Bitcoin address (not BIP21 URI string)
		if (!text.StartsWith($"{Bip21UriParser.UriScheme}:", StringComparison.OrdinalIgnoreCase))
		{
			if (NBitcoinExtensions.TryParseBitcoinAddressForNetwork(text, expectedNetwork, out BitcoinAddress? address))
			{
				return AddressStringParserResult.Ok(new AddressStringParserSuccess.BitcoinAddressParsed(address.ToString(), address));
			}

			error = Bip21UriParser.ErrorInvalidAddress;
		}
		else // Parse BIP21 URI string.
		{
			if (Bip21UriParser.TryParse(input: text, expectedNetwork, out var result, out error))
			{
				return AddressStringParserResult.Ok(
					new AddressStringParserSuccess.Bip21UriParsed(
						result.Address.ToString(),
						result.Address,
						result.Amount?.ToString() ?? "",
						result.Label ?? "",
						result.UnknownParameters.GetValueOrDefault("pj")));
			}
		}


		// Special check to verify if the provided Bitcoin address is not for a different Bitcoin network.
		if (error.IsOfSameType(Bip21UriParser.ErrorInvalidAddress))
		{
			Network networkGuess = expectedNetwork == Network.TestNet ? Network.Main : Network.TestNet;

			if (NBitcoinExtensions.TryParseBitcoinAddressForNetwork(error.Details!, networkGuess, out _))
			{
				return AddressStringParserResult.Fail(new AddressStringParserError.WrongNetwork($"Bitcoin address is valid for {networkGuess} and not for {expectedNetwork}."));
			}
		}

		try
		{
			var silentPaymentAddress = SilentPaymentAddress.Parse(text, expectedNetwork);
			return AddressStringParserResult.Ok(new AddressStringParserSuccess.SilentPaymentAddressParsed(text, silentPaymentAddress));
		}
		catch(Exception)
		{
			return AddressStringParserResult.Fail(new AddressStringParserError.GenericError("Input was not recognized as a valid destination supported by Wasabi (Bitcoin Address, Bip21 Uri, Silent Payment Address or Payjoin Endpoint"));
		}
	}
}
