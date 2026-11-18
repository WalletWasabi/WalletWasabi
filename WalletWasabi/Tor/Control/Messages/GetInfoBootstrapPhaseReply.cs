using System.Globalization;
using WalletWasabi.Tor.Control.Exceptions;
using WalletWasabi.Tor.Control.Utils;

namespace WalletWasabi.Tor.Control.Messages;

/// <summary>
/// Reply to <c>GETINFO status/bootstrap-phase</c> request.
/// </summary>
/// <seealso href="https://github.com/torproject/torspec/blob/main/control-spec.txt">3.9. GETINFO (status/bootstrap-phase) and 5.5. Status events.</seealso>
public record GetInfoBootstrapPhaseReply(int Progress, string Tag, string Summary)
{
	private const string Prefix = "status/bootstrap-phase=";

	/// <summary>Tor has built its first circuit and can serve connections.</summary>
	public bool IsBootstrapped => Progress >= 100;

	/// <remarks>Example: <c>250-status/bootstrap-phase=NOTICE BOOTSTRAP PROGRESS=0 TAG=starting SUMMARY=\"Starting\"\r\n250 OK\r\n</c>.</remarks>
	public static GetInfoBootstrapPhaseReply FromReply(TorControlReply reply)
	{
		if (!reply.Success)
		{
			throw new TorControlReplyParseException("GETINFO[bootstrap-phase]: Expected reply with OK status.");
		}

		var line = reply.ResponseLines.FirstOrDefault(l => l.StartsWith(Prefix, StringComparison.Ordinal)) ??
			throw new TorControlReplyParseException("GETINFO[bootstrap-phase]: Missing status line.");

		// Remaining part is: <severity> BOOTSTRAP PROGRESS=<n> TAG=<tag> SUMMARY="<text>" [WARNING="<text>" ...]

		// Skip "NOTICE" part.
		var remainder = line[Prefix.Length..];
		(_, remainder) = Tokenizer.ReadUntilSeparator(remainder);

		// Read "BOOTSTRAP" part.
		(var action, remainder) = Tokenizer.ReadUntilSeparator(remainder);
		if (action != "BOOTSTRAP")
		{
			throw new TorControlReplyParseException($"GETINFO[bootstrap-phase]: Expected BOOTSTRAP action, got '{action}'.");
		}

		// Read PROGRESS, TAG, and SUMMARY.
		var arguments = new Dictionary<string, string>();
		while (remainder != "")
		{
			(var key, var value, remainder) = Tokenizer.ReadKeyValueAssignment(remainder, allowValueAsQuotedString: true);

			// The separator after a quoted value stays in the remainder, so the next key starts with it.
			arguments[key.TrimStart()] = value;
		}

		if (!arguments.TryGetValue("PROGRESS", out string? progressValue))
		{
			throw new TorControlReplyParseException("GETINFO[bootstrap-phase]: Missing PROGRESS value.");
		}

		if (!int.TryParse(progressValue, NumberStyles.None, NumberFormatInfo.InvariantInfo, out int progress))
		{
			throw new TorControlReplyParseException($"GETINFO[bootstrap-phase]: Invalid PROGRESS value '{progressValue}'.");
		}

		var tag = arguments.GetValueOrDefault("TAG", string.Empty);
		var summary = arguments.GetValueOrDefault("SUMMARY", string.Empty);

		return new GetInfoBootstrapPhaseReply(progress, tag, summary);
	}
}
