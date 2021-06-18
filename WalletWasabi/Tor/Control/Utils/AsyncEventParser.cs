using System;
using WalletWasabi.Tor.Control.Exceptions;
using WalletWasabi.Tor.Control.Messages;
using WalletWasabi.Tor.Control.Messages.Events;
using WalletWasabi.Tor.Control.Messages.Events.StatusEvents;

namespace WalletWasabi.Tor.Control.Utils
{
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
				"STATUS_CLIENT" or "STATUS_SERVER" or "STATUS_GENERAL" => StatusEvent.FromReply(reply),
				"CIRC" => CircEvent.FromReply(reply),
				_ => throw new NotSupportedException("This should never happen."),
			};
		}
	}
}
