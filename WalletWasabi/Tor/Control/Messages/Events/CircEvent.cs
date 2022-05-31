using WalletWasabi.Tor.Control.Exceptions;
using WalletWasabi.Tor.Control.Messages.CircuitStatus;
using WalletWasabi.Tor.Control.Utils;

namespace WalletWasabi.Tor.Control.Messages.Events;

/// <summary>Circuit event as specified in <c>4.1.1. Circuit status changed</c> spec.</summary>
/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt"/>
public record CircEvent : IAsyncEvent
{
	public const string EventName = "CIRC";

	public CircEvent(CircuitInfo circuitInfo)
	{
		CircuitInfo = circuitInfo;
	}

	public CircuitInfo CircuitInfo { get; }

	/// <exception cref="TorControlReplyParseException"/>
	public static CircEvent FromReply(TorControlReply reply)
	{
		if (reply.StatusCode != StatusCode.AsynchronousEventNotify)
		{
			throw new TorControlReplyParseException($"CircEvent: Expected {StatusCode.AsynchronousEventNotify} status code.");
		}

		(string value, string remainder) = Tokenizer.ReadUntilSeparator(reply.ResponseLines[0]);

		if (value != EventName)
		{
			throw new TorControlReplyParseException($"CircEvent: Expected '{EventName}' event name.");
		}

		return new CircEvent(CircuitInfo.ParseLine(remainder));
	}
}
