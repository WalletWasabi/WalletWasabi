using WalletWasabi.Logging;
using WalletWasabi.Tor.Control.Exceptions;
using WalletWasabi.Tor.Control.Utils;

namespace WalletWasabi.Tor.Control.Messages.Events.OrEvents;

/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">4.1.3. OR Connection status changed</seealso>
public class OrConnEvent : IAsyncEvent
{
	public const string EventName = "ORCONN";

	public OrConnEvent(string target, OrStatus orStatus, Reason reason, int? nCircs, string? connId)
	{
		Target = target;
		OrStatus = orStatus;
		Reason = reason;
		NCircs = nCircs;
		ConnId = connId;
	}

	public string Target { get; }
	public OrStatus OrStatus { get; }
	public Reason Reason { get; }
	public int? NCircs { get; }
	public string? ConnId { get; }

	/// <exception cref="TorControlReplyParseException"/>
	public static OrConnEvent FromReply(TorControlReply reply)
	{
		if (reply.StatusCode != StatusCode.AsynchronousEventNotify)
		{
			throw new TorControlReplyParseException($"{nameof(OrConnEvent)}: Expected {StatusCode.AsynchronousEventNotify} status code.");
		}

		(string value, string remainder) = Tokenizer.ReadUntilSeparator(reply.ResponseLines[0]);

		if (value != EventName)
		{
			throw new TorControlReplyParseException($"{nameof(OrConnEvent)}: Expected '{EventName}' event name.");
		}

		string target;
		OrStatus orStatus = OrStatus.UNKNOWN;
		Reason reason = Reason.UNKNOWN;
		int? nCircs = null;
		string? connId = null;

		// Mandatory piece of information per spec.
		(target, remainder) = Tokenizer.ReadUntilSeparator(remainder);
		(value, remainder) = Tokenizer.ReadUntilSeparator(remainder);
		orStatus = Tokenizer.ParseEnumValue(value, OrStatus.UNKNOWN);

		// Optional arguments.
		while (remainder != "")
		{
			string key;
			(key, value, remainder) = Tokenizer.ReadKeyValueAssignment(remainder, allowValueAsQuotedString: true);

			if (key == "REASON")
			{
				reason = Tokenizer.ParseEnumValue(value, Reason.UNKNOWN);
				continue;
			}
			else if (key == "NCIRCS")
			{
				nCircs = int.Parse(value);
				continue;
			}
			else if (key == "ID")
			{
				connId = value;
				continue;
			}

			Logger.LogError($"Failed to handle '{remainder}'.");
			break;
		}

		return new OrConnEvent(target, orStatus, reason, nCircs, connId);
	}

	/// <inheritdoc/>
	public override string ToString()
	{
		string args = string.Join(
			separator: ", ",
			$"{nameof(Target)}='{Target}'",
			$"{nameof(OrStatus)}={OrStatus}",
			$"{nameof(Reason)}={Reason}",
			$"{nameof(NCircs)}={(NCircs.HasValue ? NCircs : "null")}",
			$"{nameof(ConnId)}={ConnId ?? "null"}");

		return $"{nameof(OrConnEvent)}{{{args}}}";
	}
}
