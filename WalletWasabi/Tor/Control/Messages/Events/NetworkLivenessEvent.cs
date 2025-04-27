using WalletWasabi.Tor.Control.Exceptions;
using WalletWasabi.Tor.Control.Utils;

namespace WalletWasabi.Tor.Control.Messages.Events;

/// <summary>Network liveness event as specified in <c>4.1.27. Network liveness has changed</c> spec.</summary>
/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt"/>
public record NetworkLivenessEvent : IAsyncEvent
{
	public const string EventName = "NETWORK_LIVENESS";

	private const string StatusUp = "UP";
	private const string StatusDown = "DOWN";

	public NetworkLivenessEvent(string status)
	{
		Status = status;
	}

	public string Status { get; }

	/// <exception cref="TorControlReplyParseException"/>
	public static NetworkLivenessEvent FromReply(TorControlReply reply)
	{
		if (reply.StatusCode != StatusCode.AsynchronousEventNotify)
		{
			throw new TorControlReplyParseException($"{nameof(NetworkLivenessEvent)}: Expected {StatusCode.AsynchronousEventNotify} status code.");
		}

		(string value, string remainder) = Tokenizer.ReadUntilSeparator(reply.ResponseLines[0]);

		if (value != EventName)
		{
			throw new TorControlReplyParseException($"{nameof(NetworkLivenessEvent)}: Expected '{EventName}' event name.");
		}

		(value, remainder) = Tokenizer.ReadUntilSeparator(remainder);

		// Spec says: Controllers MUST tolerate unrecognized status types.
		return new NetworkLivenessEvent(value);
	}
}
