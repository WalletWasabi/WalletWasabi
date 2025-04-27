using NBitcoin;
using NBitcoin.Payment;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Web;
using WalletWasabi.Extensions;

namespace WalletWasabi.Userfacing.Bip21;

/// <summary>
/// BIP21 URI parser.
/// </summary>
/// <seealso href="https://github.com/bitcoin/bips/blob/master/bip-0021.mediawiki"/>
/// <seealso cref="BitcoinUrlBuilder">Inspired by NBitcoin's implementation.</seealso>
public class Bip21UriParser
{
	/// <summary>URI scheme of all BIP21 URIs.</summary>
	/// <remarks>
	/// BIP mandates that:
	/// The scheme component ("bitcoin:") is case-insensitive, and implementations must accept any combination of uppercase and lowercase letters.
	/// </remarks>
	/// <seealso href="https://github.com/bitcoin/bips/blob/master/bip-0021.mediawiki#abnf-grammar"/>
	public const string UriScheme = "bitcoin";

	public static readonly Error ErrorInvalidUri = new(Code: 1, "Not a valid absolute URI.");
	public static readonly Error ErrorInvalidUriScheme = new(Code: 2, $"Expected '{UriScheme}' scheme.");
	public static readonly Error ErrorMissingAddress = new(Code: 3, "Bitcoin address is missing.");
	public static readonly Error ErrorInvalidAddress = new(Code: 4, "Invalid Bitcoin address.");
	public static readonly Error ErrorInvalidUriQuery = new(Code: 5, "Not a valid absolute URI.");
	public static readonly Error ErrorDuplicateParameter = new(Code: 6, "Parameter can be specified just once.");
	public static readonly Error ErrorMissingAmountValue = new(Code: 7, "Missing amount value.");
	public static readonly Error ErrorInvalidAmountValue = new(Code: 8, "Invalid amount value.");
	public static readonly Error ErrorUnsupportedReqParameter = new(Code: 9, "Unsupported required parameter found.");

	public static bool TryParse(string input, Network network, [NotNullWhen(true)] out Result? result, [NotNullWhen(false)] out Error? error)
	{
		result = null;
		error = null;

		if (!Uri.TryCreate(input, UriKind.Absolute, out Uri? parsedUri))
		{
			error = ErrorInvalidUri with { Details = input };
			return false;
		}

		if (!parsedUri.Scheme.Equals(UriScheme, StringComparison.OrdinalIgnoreCase))
		{
			error = ErrorInvalidUriScheme with { Details = parsedUri.Scheme };
			return false;
		}

		if (parsedUri.AbsolutePath is not { Length: > 0 } addressString)
		{
			error = ErrorMissingAddress;
			return false;
		}

		Money? amount = null;
		string? label = null;
		string? message = null;

		var addressParsingResult = AddressParser.ParseBitcoinAddress(addressString, network);
		if (!addressParsingResult.IsOk)
		{
			error = ErrorInvalidAddress with { Details = addressString };
			return false;
		}

		Dictionary<string, string> unknownParameters = new();
		NameValueCollection queryParameters = HttpUtility.ParseQueryString(parsedUri.Query);

		foreach (string? parameterName in queryParameters.AllKeys)
		{
			if (parameterName is null)
			{
				continue;
			}

			string? value = queryParameters[parameterName];

			if (value is null)
			{
				continue;
			}

			if (parameterName == "amount")
			{
				if (amount is not null)
				{
					error = ErrorDuplicateParameter with { Details = parameterName };
					return false;
				}

				if (value.Trim() == "")
				{
					error = ErrorMissingAmountValue with { Details = value };
					return false;
				}

				if (!Money.TryParse(value, out amount))
				{
					error = ErrorInvalidAmountValue with { Details = value };
					return false;
				}
			}
			else if (parameterName == "label")
			{
				if (label is not null)
				{
					error = ErrorDuplicateParameter with { Details = parameterName };
					return false;
				}

				label = value;
			}
			else if (parameterName == "message")
			{
				if (message is not null)
				{
					error = ErrorDuplicateParameter with { Details = parameterName };
					return false;
				}

				message = value;
			}
			else if (parameterName.StartsWith("req-", StringComparison.Ordinal))
			{
				error = ErrorUnsupportedReqParameter with { Details = parameterName };
				return false;
			}
			else
			{
				unknownParameters.Add(parameterName, value);
			}
		}

		result = new(Uri: parsedUri, network, addressParsingResult.Value, amount, Label: label, Message: message, unknownParameters);
		return true;
	}

	/// <summary>
	/// Successful result of parsing a BIP21 URI string.
	/// </summary>
	public record Result( Uri Uri, Network Network, Address Address, Money? Amount, string? Label, string? Message, Dictionary<string, string> UnknownParameters);

	/// <summary>
	/// Error result of parsing a BIP21 URI string.
	/// </summary>
	/// <param name="Code">Unique code of the error.</param>
	/// <param name="Message">Generic message of the error (with no user-provided data).</param>
	/// <param name="Details">Optionally, context information. For example, if the address part of a BIP21 URI string is malformed, the string is to stored here.</param>
	public record Error(int Code, string Message, string? Details = null);
}
