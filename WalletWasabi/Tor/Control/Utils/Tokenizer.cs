using System;
using WalletWasabi.Tor.Control.Exceptions;

namespace WalletWasabi.Tor.Control.Utils
{
	public class Tokenizer
	{
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
		/// Reads "&lt;KEY&gt;=QuotedString".
		/// </summary>
		/// <param name="key"></param>
		/// <param name="input"></param>
		/// <returns>Quoted string content.</returns>
		public static (string value, string remainder) ReadKeyValueAssignment(string key, string input)
		{
			input = ReadExactString(key, input);

			int startAt = input.IndexOf('"');

			if (startAt == -1)
			{
				throw new TorControlReplyParseException("Missing opening quote character.");
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

		public static string ReadExactString(string expectedStart, string input)
		{
			if (!input.StartsWith(expectedStart, StringComparison.Ordinal))
			{
				throw new TorControlReplyParseException($"Expected input to start with '{expectedStart}'.");
			}

			return input[expectedStart.Length..];
		}
	}
}
