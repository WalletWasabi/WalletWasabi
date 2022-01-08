using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Control.Exceptions;
using WalletWasabi.Tor.Control.Utils;

namespace WalletWasabi.Tor.Control.Messages;

/// <remarks>
/// Note that the protocol_version is the only mandatory data for a valid PROTOCOLINFO response.
/// </remarks>
public record ProtocolInfoReply
{
	private const string LineTypeProtocolInfo = "PROTOCOLINFO";
	private const string LineTypeVersion = "VERSION";
	private const string LineTypeAuth = "AUTH";

	/// <summary>Always <c>1</c>.</summary>
	public int? ProtocolVersion { get; init; }

	/// <example>0.4.5.7</example>
	public string? TorVersion { get; init; }

	/// <summary>Full path to cookie file.</summary>
	public string? CookieFilePath { get; init; }

	public ImmutableArray<string> AuthMethods { get; init; }

	/// <exception cref="TorControlReplyParseException"/>
	public static ProtocolInfoReply FromReply(TorControlReply reply)
	{
		if (!reply.Success || reply.ResponseLines.Last() != "250 OK")
		{
			throw new TorControlReplyParseException("PROTOCOLINFO: Expected reply with OK status.");
		}

		// Mandatory piece of information per spec.
		int protocolVersion = ParseProtocolVersionLine(reply.ResponseLines[0]);

		string? torVersion = null;
		string? cookieFilePath = null;
		List<string> authMethods = new();

		// Optional pieces of information per spec.
		foreach (string line in reply.ResponseLines.Skip(1).SkipLast(1))
		{
			(string token, string remainder) = Tokenizer.ReadUntilSeparator(line);

			if (token == LineTypeVersion)
			{
				// VersionLine = "250-VERSION" SP "Tor=" TorVersion OptArguments CRLF
				// TorVersion = QuotedString
				(string value, _) = Tokenizer.ReadKeyQuotedValueAssignment(key: "Tor", remainder);

				torVersion = value;
			}
			else if (token == LineTypeAuth)
			{
				remainder = Tokenizer.ReadExactString("METHODS=", remainder);
				(string methods, string optCookieFileRemainder) = Tokenizer.ReadUntilSeparator(remainder);
				authMethods.AddRange(methods.Split(','));

				if (optCookieFileRemainder.StartsWith("COOKIEFILE=", StringComparison.Ordinal))
				{
					(cookieFilePath, _) = Tokenizer.ReadKeyQuotedValueAssignment(key: "COOKIEFILE", optCookieFileRemainder);
				}
			}
		}

		return new ProtocolInfoReply()
		{
			ProtocolVersion = protocolVersion,
			TorVersion = torVersion,
			AuthMethods = authMethods.ToImmutableArray(),
			CookieFilePath = cookieFilePath,
		};
	}

	private static int ParseProtocolVersionLine(string line)
	{
		(string token, string remainder) = Tokenizer.ReadUntilSeparator(line);

		if (token == LineTypeProtocolInfo)
		{
			if (int.TryParse(remainder, out int parsedVersion))
			{
				if (parsedVersion != 1)
				{
					Logger.LogWarning($"Version {parsedVersion} may not work expected.");
				}

				return parsedVersion;
			}
			else
			{
				Logger.LogError("Failed to parse PROTOCOLINFO version.");
			}
		}

		throw new TorControlReplyParseException("PROTOCOLINFO: Version is not specified.");
	}
}
