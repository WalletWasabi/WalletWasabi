using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WalletWasabi.Tor.Control.Exceptions;
using WalletWasabi.Tor.Control.Utils;

namespace WalletWasabi.Tor.Control.Messages;

/// <summary>
/// Reply to <c>GETINFO status/bootstrap-phase</c>: the most recent bootstrap status event, for example
/// <c>status/bootstrap-phase=NOTICE BOOTSTRAP PROGRESS=100 TAG=done SUMMARY="Done"</c>.
/// </summary>
/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">3.9. GETINFO (status/bootstrap-phase) and 5.5. Status events.</seealso>
public record GetInfoBootstrapPhaseReply(int Progress, string Tag, string Summary)
{
	private const string Prefix = "status/bootstrap-phase=";

	/// <summary>Tor has built its first circuit and can serve connections.</summary>
	public bool IsBootstrapped => Progress >= 100;

	/// <exception cref="TorControlReplyParseException"/>
	public static GetInfoBootstrapPhaseReply FromReply(TorControlReply reply)
	{
		if (!reply.Success)
		{
			throw new TorControlReplyParseException("GETINFO[status/bootstrap-phase]: Expected reply with OK status.");
		}

		string? line = reply.ResponseLines.FirstOrDefault(l => l.StartsWith(Prefix, StringComparison.Ordinal));
		if (line is null)
		{
			throw new TorControlReplyParseException("GETINFO[status/bootstrap-phase]: Missing status line.");
		}

		// <severity> BOOTSTRAP PROGRESS=<n> TAG=<tag> SUMMARY="<text>" [WARNING="<text>" ...]
		string remainder = line[Prefix.Length..];
		(_, remainder) = Tokenizer.ReadUntilSeparator(remainder);

		string action;
		(action, remainder) = Tokenizer.ReadUntilSeparator(remainder);
		if (action != "BOOTSTRAP")
		{
			throw new TorControlReplyParseException($"GETINFO[status/bootstrap-phase]: Expected BOOTSTRAP action, got '{action}'.");
		}

		Dictionary<string, string> arguments = new();
		while (remainder != string.Empty)
		{
			string key;
			string value;
			(key, value, remainder) = Tokenizer.ReadKeyValueAssignment(remainder, allowValueAsQuotedString: true);

			// The separator after a quoted value stays in the remainder, so the next key starts with it.
			arguments[key.TrimStart()] = value;
		}

		if (!arguments.TryGetValue("PROGRESS", out string? progressValue)
			|| !int.TryParse(progressValue, NumberStyles.None, NumberFormatInfo.InvariantInfo, out int progress))
		{
			throw new TorControlReplyParseException("GETINFO[status/bootstrap-phase]: Missing or invalid PROGRESS.");
		}

		return new GetInfoBootstrapPhaseReply(
			progress,
			arguments.GetValueOrDefault("TAG", string.Empty),
			arguments.GetValueOrDefault("SUMMARY", string.Empty));
	}
}
