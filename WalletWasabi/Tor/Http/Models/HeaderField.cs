using System.IO;
using System.Threading.Tasks;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using static WalletWasabi.Tor.Http.Constants;

namespace WalletWasabi.Tor.Http.Models;

public class HeaderField
{
	public HeaderField(string name, string value)
	{
		Name = name;

		value = CorrectObsFolding(value);
		Value = value;
	}

	/// <remarks>Note that comparison of header names must be case-insensitive. <see cref="IsNameEqual(string)"/>.</remarks>
	public string Name { get; private set; }

	public string Value { get; private set; }

	/// <summary>Compares correctly two header names.</summary>
	/// <remarks>Header names are case-insensitive. Note that in HTTP/2, lower-case names are preferred.</remarks>
	public bool IsNameEqual(string name)
	{
		return Name.Equals(name, StringComparison.OrdinalIgnoreCase);
	}

	public static string CorrectObsFolding(string text)
	{
		// fix obs line folding
		// https://tools.ietf.org/html/rfc7230#section-3.2.4
		// replace each received obs-fold with one or more SP octets prior to interpreting the field value
		text = text.Replace(CRLF + SP, SP + SP);
		text = text.Replace(CRLF + HTAB, SP + HTAB);
		return text;
	}

	// https://tools.ietf.org/html/rfc7230#section-3.2
	// Each header field consists of a case-insensitive field name followed
	// by a colon(":"), optional leading whitespace, the field value, and
	// optional trailing whitespace.
	// header-field   = field-name ":" OWS field-value OWS
	// The OWS rule is used where zero or more linear whitespace octets	might appear.
	public string ToString(bool endWithCRLF)
	{
		var ret = $"{Name}:{Value}";
		if (endWithCRLF)
		{
			ret += CRLF;
		}
		return ret;
	}

	public override string ToString()
	{
		return ToString(true);
	}

	public static async Task<HeaderField> CreateNewAsync(string fieldString)
	{
		fieldString = fieldString.TrimEnd(CRLF, StringComparison.Ordinal);

		using var reader = new StringReader(fieldString);
		var name = reader.ReadPart(':');

		// if empty
		if (string.IsNullOrEmpty(name?.Trim()))
		{
			throw new FormatException($"Wrong {nameof(HeaderField)}: {fieldString}.");
		}

		// https://tools.ietf.org/html/rfc7230#section-3.2.4
		// No whitespace is allowed between the header field-name and colon.
		// A proxy MUST remove any such whitespace from a response message before forwarding the message downstream.
		name = name.TrimEnd();

		// whitespace not allowed
		if (name != name.Trim())
		{
			throw new FormatException($"Wrong {nameof(HeaderField)}: {fieldString}.");
		}

		var value = await reader.ReadToEndAsync().ConfigureAwait(false);
		value = Guard.Correct(value);

		return new HeaderField(name, value);
	}
}
