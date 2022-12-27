using WalletWasabi.Tor.Control.Exceptions;

namespace WalletWasabi.Tor.Control.Utils;

/// <summary>
/// Helper functions for parsing Tor control protocol replies.
/// </summary>
/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">See 2.3. Replies from Tor to the controller</seealso>
public static class Tokenizer
{
	/// <summary>
	/// Splits input string using space separator (" ") into at most two parts - token and remainder.
	/// </summary>
	public static (string token, string remainder) ReadUntilSeparator(string input)
	{
		if (input == "")
		{
			throw new TorControlReplyParseException("Expected a token.");
		}

		string[] parts = input.Split(separator: ' ', count: 2);

		if (parts.Length == 2)
		{
			return (parts[0], parts[1]);
		}
		else
		{
			return (parts[0], "");
		}
	}

	/// <summary>
	/// Reads <c>&lt;KEY&gt;=&lt;VALUE&gt;</c>.
	/// </summary>
	/// <param name="allowValueAsQuotedString">If <c>true</c>, reads <c>&lt;KEY&gt;=QuotedString</c>.</param>
	public static (string key, string value, string remainder) ReadKeyValueAssignment(string input, bool allowValueAsQuotedString = false)
	{
		int valueStartAt = input.IndexOf('=');

		if (valueStartAt == -1)
		{
			throw new TorControlReplyParseException("Missing equal sign ('=').");
		}

		string key = input[0..valueStartAt];
		string remainder = input[(valueStartAt + 1)..];
		string value;

		if (remainder.Length == 0)
		{
			value = "";
		}
		else if (allowValueAsQuotedString && remainder.Length > 0 && remainder[0] == '"')
		{
			(value, remainder) = ReadQuotedString(remainder);
		}
		else
		{
			(value, remainder) = ReadUntilSeparator(remainder);
		}

		return (key, value, remainder);
	}

	/// <summary>
	/// Reads <c>&lt;KEY&gt;=QuotedString</c> string from <paramref name="input"/>.
	/// </summary>
	/// <returns>Quoted string content.</returns>
	public static (string value, string remainder) ReadKeyQuotedValueAssignment(string key, string input)
	{
		input = ReadExactString(key, input);
		input = ReadExactString("=", input);
		(string value, string remainder) = ReadQuotedString(input);

		if (remainder != "")
		{
			remainder = remainder[1..];
		}

		return (value, remainder);
	}

	public static (string value, string remainder) ReadQuotedString(string input)
	{
		int startAt = input.IndexOf('"');

		if (startAt != 0)
		{
			throw new TorControlReplyParseException("Quote character must be the first character in the input.");
		}

		startAt++;
		int endAt = startAt;

		if (endAt >= input.Length)
		{
			throw new TorControlReplyParseException("Unexpected end of string.");
		}

		// Do not stop at escaped quotes (").
		while ((endAt = input.IndexOf('"', endAt)) != -1)
		{
			if (input[endAt - 1] != '\\')
			{
				break;
			}
		}

		if (endAt == -1)
		{
			throw new TorControlReplyParseException($"Missing closing quote character.");
		}

		string value = input[startAt..endAt];
		string remainder = (endAt == input.Length) ? "" : input[(endAt + 1)..];

		// Unescape: \\ -> '
		value = value.Replace(@"\\", @"\");

		// Unescape: \" -> "
		value = value.Replace("\\\"", "\"");

		return (value, remainder);
	}

	/// <summary>
	/// Makes sure that <paramref name="input"/> starts with <paramref name="expectedStart"/>.
	/// </summary>
	public static string ReadExactString(string expectedStart, string input)
	{
		if (!input.StartsWith(expectedStart, StringComparison.Ordinal))
		{
			throw new TorControlReplyParseException($"Expected input to start with '{expectedStart}'.");
		}

		return input[expectedStart.Length..];
	}

	/// <summary>Parses a string value to an enum value.</summary>
	/// <remarks>Tor spec mandates (in general) that unknown values cannot lead to crash of a Tor control parser.</remarks>
	public static T ParseEnumValue<T>(string value, T defaultValue) where T : struct
	{
		if (!Enum.TryParse(value, out T result))
		{
			result = defaultValue;
		}

		return result;
	}
}
