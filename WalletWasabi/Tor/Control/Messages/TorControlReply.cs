using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WalletWasabi.Tor.Control.Messages;

/// <summary>
/// A class containing information regarding a response received back from a Tor control connection.
/// </summary>
/// <remarks>
/// Tor replies follow the following grammar:
/// <code>
///   Reply = SyncReply / AsyncReply
///   SyncReply = *(MidReplyLine / DataReplyLine) EndReplyLine
///   AsyncReply = *(MidReplyLine / DataReplyLine) EndReplyLine
///
///   MidReplyLine = StatusCode "-" ReplyLine
///   DataReplyLine = StatusCode "+" ReplyLine CmdData
///   EndReplyLine = StatusCode SP ReplyLine
///   ReplyLine = [ReplyText] CRLF
///   ReplyText = XXXX
///   StatusCode = 3DIGIT
/// </code>
/// </remarks>
/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">Section 2.3</seealso>
public record TorControlReply : ITorControlReply
{
	private static readonly ReadOnlyCollection<string> NoLines = new List<string>().AsReadOnly();

	public TorControlReply(StatusCode statusCode)
	{
		StatusCode = statusCode;
		ResponseLines = NoLines;
	}

	public TorControlReply(StatusCode statusCode, IList<string> responseLines)
	{
		StatusCode = statusCode;
		ResponseLines = new ReadOnlyCollection<string>(responseLines);
	}

	public ReadOnlyCollection<string> ResponseLines { get; }

	public StatusCode StatusCode { get; }

	/// <summary>Gets a value indicating whether the reply indicated success.</summary>
	public bool Success => StatusCode == StatusCode.OK;

	public static implicit operator bool(TorControlReply r) => r.Success;

	/// <inheritdoc/>
	public override string ToString()
	{
		return string.Format("[{0}: '{1}']", StatusCode, string.Join("<CRLF>", ResponseLines));
	}
}
