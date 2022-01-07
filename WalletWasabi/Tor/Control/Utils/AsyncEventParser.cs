using WalletWasabi.Tor.Control.Exceptions;
using WalletWasabi.Tor.Control.Messages;
using WalletWasabi.Tor.Control.Messages.Events;
using WalletWasabi.Tor.Control.Messages.Events.OrEvents;
using WalletWasabi.Tor.Control.Messages.Events.StatusEvents;

namespace WalletWasabi.Tor.Control.Utils;

/// <summary>Parses an incoming Tor control event based on its name.</summary>
public static class AsyncEventParser
{
	/// <exception cref="TorControlReplyParseException"/>
	public static IAsyncEvent Parse(TorControlReply reply)
	{
		if (reply.StatusCode != StatusCode.AsynchronousEventNotify)
		{
			throw new TorControlReplyParseException($"Event: Expected {StatusCode.AsynchronousEventNotify} status code.");
		}

		(string value, _) = Tokenizer.ReadUntilSeparator(reply.ResponseLines[0]);

		return value switch
		{
			StatusEvent.EventNameStatusClient or StatusEvent.EventNameStatusServer or StatusEvent.EventNameStatusGeneral => StatusEvent.FromReply(reply),
			CircEvent.EventName => CircEvent.FromReply(reply),
			NetworkLivenessEvent.EventName => NetworkLivenessEvent.FromReply(reply),
			OrConnEvent.EventName => OrConnEvent.FromReply(reply),
			_ => throw new NotSupportedException("This should never happen."),
		};
	}
}
