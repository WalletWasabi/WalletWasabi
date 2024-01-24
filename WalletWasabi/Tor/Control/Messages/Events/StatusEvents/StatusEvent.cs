using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Tor.Control.Exceptions;
using WalletWasabi.Tor.Control.Utils;

namespace WalletWasabi.Tor.Control.Messages.Events.StatusEvents;

public enum StatusType
{
	STATUS_GENERAL,
	STATUS_CLIENT,
	STATUS_SERVER
}

public enum StatusSeverity
{
	NOTICE,
	WARN,
	ERR
}

/// <summary>Base representation of a status event as specified in <c>4.1.10. Status events</c> spec.</summary>
/// <remarks>
/// Grammar is as follows:
/// <code>
/// "650" SP StatusType SP StatusSeverity SP StatusAction
///                                      [SP StatusArguments] CRLF
///
/// StatusType = "STATUS_GENERAL" / "STATUS_CLIENT" / "STATUS_SERVER"
/// StatusSeverity = "NOTICE" / "WARN" / "ERR"
/// StatusAction = 1*ALPHA
/// StatusArguments = StatusArgument *(SP StatusArgument)
/// StatusArgument = StatusKeyword '=' StatusValue
/// StatusKeyword = 1*(ALNUM / "_")
/// StatusValue = 1*(ALNUM / '_')  / QuotedString
/// </code>
/// </remarks>
/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt"/>
public record StatusEvent : IAsyncEvent
{
	public const string EventNameStatusGeneral = "STATUS_GENERAL";
	public const string EventNameStatusClient = "STATUS_CLIENT";
	public const string EventNameStatusServer = "STATUS_SERVER";

	/// <remarks>
	/// From spec: This event will only be sent if Tor just built a circuit that changed our mind --
	/// that is, prior to this event we didn't know whether we could establish circuits.
	/// <para>Suggested use: Controllers can notify their users that Tor is ready for use as a client once they see this status event.</para>
	/// </remarks>
	public const string ActionCircuitEstablished = "CIRCUIT_ESTABLISHED";

	/// <remarks>
	/// From spec: We are no longer confident that we can build circuits. The "reason" keyword provides an explanation:
	/// which other status event type caused our lack of confidence.
	/// <para>Suggested use: Controllers may want to use this event to decide when to indicate progress
	/// to their users, but should not interrupt the user's browsing to do so.</para>
	/// </remarks>
	public const string ActionCircuitNotEstablished = "CIRCUIT_NOT_ESTABLISHED";

	public StatusEvent(string action, Dictionary<string, string> arguments)
	{
		Action = action;
		Arguments = arguments;
	}

	public StatusType Type { get; init; }

	public StatusSeverity Severity { get; init; }

	public string Action { get; init; }

	public Dictionary<string, string> Arguments { get; init; }

	/// <exception cref="TorControlReplyParseException"/>
	public static StatusEvent FromReply(TorControlReply reply)
	{
		if (reply.StatusCode != StatusCode.AsynchronousEventNotify)
		{
			throw new TorControlReplyParseException($"{nameof(StatusEvent)}: Expected {StatusCode.AsynchronousEventNotify} status code.");
		}

		string remainder = reply.ResponseLines[0];
		string value;

		(value, remainder) = Tokenizer.ReadUntilSeparator(remainder);

		if (!Enum.TryParse(value, out StatusType statusType))
		{
			throw new TorControlReplyParseException($"{nameof(StatusEvent)}: Unknown status type.");
		}

		(value, remainder) = Tokenizer.ReadUntilSeparator(remainder);

		if (!Enum.TryParse(value, out StatusSeverity statusSeverity))
		{
			throw new TorControlReplyParseException($"{nameof(StatusEvent)}: Unknown status type.");
		}

		// Mandatory piece of information per spec.
		string action;
		(action, remainder) = Tokenizer.ReadUntilSeparator(remainder);

		Dictionary<string, string> arguments = new();

		// Optional pieces of information per spec.
		while (remainder != string.Empty)
		{
			string key;
			(key, value, remainder) = Tokenizer.ReadKeyValueAssignment(remainder, allowValueAsQuotedString: true);

			if (!arguments.TryAdd(key, value))
			{
				throw new TorControlReplyParseException($"{nameof(StatusEvent)}: Argument already defined.");
			}
		}

		if (action == "BOOTSTRAP")
		{
			return new BootstrapStatusEvent(action, arguments)
			{
				Type = statusType,
				Severity = statusSeverity,
			};
		}
		else
		{
			return new StatusEvent(action, arguments)
			{
				Type = statusType,
				Severity = statusSeverity,
			};
		}
	}

	/// <inheritdoc/>
	public override string ToString()
	{
		string prettyArguments = string.Join(", ", Arguments.Select(kv => $"{kv.Key}={kv.Value}"));
		return $"{nameof(StatusEvent)}{{{nameof(Type)}={Type}, {nameof(Severity)}={Severity}, {nameof(Action)}={Action}, {nameof(Arguments)}={{{prettyArguments}}}}}";
	}
}
