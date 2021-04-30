using System;
using System.Linq;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Control.Exceptions;

namespace WalletWasabi.Tor.Control.Messages
{
	/// <remarks>
	/// Note that the protocol_version is the only mandatory data for a valid PROTOCOLINFO response.
	/// </remarks>
	public class ProtocolInfoReply
	{
		private const string ProtocolInfoLinePrefix = "PROTOCOLINFO ";

		private const string VersionLinePrefix = "VERSION ";

		public int? ProtocolVersion { get; init; }
		public string? TorVersion { get; init; }
		public string? CookieFile { get; init; }

		// public AuthMethod[] AuthMethods { get; init; }

		public static ProtocolInfoReply FromReply(TorControlReply reply)
		{
			if (!reply.Success || reply.ResponseLines.Last() != "250 OK")
			{
				throw new TorControlReplyParseException("PROTOCOLINFO: Expected reply with OK status.");
			}

			int protocolVersion = ParseProtocolVersionLine(reply.ResponseLines[0]);

			foreach (string line in reply.ResponseLines.Skip(1).SkipLast(1))
			{
				if (line.StartsWith(VersionLinePrefix, StringComparison.Ordinal))
				{

				}
			}

			return new ProtocolInfoReply()
			{
				ProtocolVersion = protocolVersion
			};
		}

		private static int ParseProtocolVersionLine(string line)
		{
			if (line.StartsWith(ProtocolInfoLinePrefix, StringComparison.Ordinal))
			{
				if (int.TryParse(line.AsSpan(ProtocolInfoLinePrefix.Length), out int parsedVersion))
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
}
