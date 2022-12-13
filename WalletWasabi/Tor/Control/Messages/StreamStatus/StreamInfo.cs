using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Control.Exceptions;
using WalletWasabi.Tor.Control.Utils;

namespace WalletWasabi.Tor.Control.Messages.StreamStatus;

/// <summary>Implemented as specified in <c>4.1.2. Stream status changed</c> spec.</summary>
/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt"/>
public record StreamInfo
{
	public StreamInfo(string streamID, StreamStatusFlag streamStatus, string circuitID, string targetAddress, int port)
	{
		StreamID = streamID;
		StreamStatus = streamStatus;
		CircuitID = circuitID;
		TargetAddress = targetAddress;
		Port = port;
		IsoFields = Array.Empty<IsoFieldFlag>();
	}

	/// <summary>Unique stream identifier.</summary>
	/// <remarks>
	/// Currently, Tor only uses digits, but this may change.
	/// <para>String matches <c>^[a-zA-Z0-9]{1,16}$</c>.</para>
	/// </remarks>
	/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">2.4. General-use tokens</seealso>
	public string StreamID { get; }

	/// <summary>Status of the Tor stream.</summary>
	public StreamStatusFlag StreamStatus { get; }

	/// <summary>
	/// The circuit ID designates which circuit this stream is attached to.
	/// <para>If the stream is unattached, the circuit ID <c>0</c> is given.</para>
	/// </summary>
	/// <seealso cref="CircuitStatus.CircuitInfo.CircuitID"/>
	public string CircuitID { get; }

	/// <remarks>Parsed part of the original "target" value.</remarks>
	public string TargetAddress { get; }

	/// <remarks>Parsed part of the original "target" value.</remarks>
	public int Port { get; }

	public Reason? Reason { get; init; }
	public Reason? RemoteReason { get; init; }

	/// <remarks>Possible values are: <c>CACHE</c> or <c>EXIT</c>.</remarks>
	public string? Source { get; init; }

	/// <remarks>In form <c>Address:Port</c>.</remarks>
	public string? SourceAddr { get; init; }
	public Purpose? Purpose { get; init; }
	public string? UserName { get; init; }
	public string? UserPassword { get; init; }
	public ClientProtocol? ClientProtocol { get; init; }

	/// <summary>Field indicates the nym epoch that was active when a client initiated this stream.</summary>
	/// <remarks>
	/// The epoch increments when the <c>NEWNYM</c> signal is received.
	/// Streams with different nym epochs are isolated on separate circuits.
	/// <para>Value is a nonnegative integer.</para>
	/// </remarks>
	public int? NymEpoch { get; init; }

	/// <summary>Field indicates the session group of the listener port that a client used to initiate this stream.</summary>
	/// <remarks>
	/// By default, the session group is different for each listener port, but this can be overridden for a listener
	/// via the <c>SessionGroup</c> option in <c>torrc</c>.
	/// Streams with different session groups are isolated on separate circuits.
	/// <para>Value can be any integer.</para>
	/// </remarks>
	public int? SessionGroup { get; init; }

	/// <summary>
	/// Field indicates the set of STREAM event fields for which stream isolation is enabled for the listener port
	/// that a client used to initiate this stream.
	/// </summary>
	public IReadOnlyList<IsoFieldFlag> IsoFields { get; init; }

	public static StreamInfo ParseLine(string line)
	{
		(string streamId, string remainder1) = Tokenizer.ReadUntilSeparator(line);
		(string circuitStatus, string remainder2) = Tokenizer.ReadUntilSeparator(remainder1);
		(string circuitId, string remainder3) = Tokenizer.ReadUntilSeparator(remainder2);
		(string target, string remainder4) = Tokenizer.ReadUntilSeparator(remainder3);

		int targetIndex = target.LastIndexOf(':');

		if (targetIndex == -1)
		{
			throw new TorControlReplyParseException($"{nameof(StreamInfo)} should contain 'target' value with a colon (':').");
		}

		string targetAddress = target[0..targetIndex];

		if (!int.TryParse(target[(targetIndex + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out int targetPort))
		{
			throw new TorControlReplyParseException($"{nameof(StreamInfo)} should contain 'target' with a valid port number.");
		}

		if (targetPort < 0 || targetPort > 65535)
		{
			throw new TorControlReplyParseException($"{nameof(StreamInfo)}: Port value ({targetPort} is out of range.");
		}

		StreamStatusFlag streamStatus = Tokenizer.ParseEnumValue(circuitStatus, StreamStatusFlag.UNKNOWN);

		string remainder = remainder4;

		Reason? reason = null;
		Reason? remoteReason = null;
		string? source = null;
		string? sourceAddr = null;
		Purpose? purpose = null;
		string? userName = null;
		string? userPassword = null;
		ClientProtocol? clientProtocol = null;
		int? nymEpoch = null;
		int? sessionGroup = null;
		List<IsoFieldFlag> isoFields = new();

		// Optional arguments.
		while (remainder != "")
		{
			if (remainder.StartsWith("SOURCE=", StringComparison.Ordinal))
			{
				(string _, source, remainder) = Tokenizer.ReadKeyValueAssignment(remainder);
				continue;
			}
			else if (remainder.StartsWith("SOCKS_USERNAME=", StringComparison.Ordinal))
			{
				(userName, remainder) = Tokenizer.ReadKeyQuotedValueAssignment(key: "SOCKS_USERNAME", remainder);
				continue;
			}
			else if (remainder.StartsWith("SOCKS_PASSWORD=", StringComparison.Ordinal))
			{
				(userPassword, remainder) = Tokenizer.ReadKeyQuotedValueAssignment(key: "SOCKS_PASSWORD", remainder);
				continue;
			}
			else if (remainder.StartsWith("NYM_EPOCH=", StringComparison.Ordinal))
			{
				(_, string nymEpochStr, remainder) = Tokenizer.ReadKeyValueAssignment(remainder);

				if (int.TryParse(nymEpochStr, out int nymEpochInt))
				{
					nymEpoch = nymEpochInt;
				}

				continue;
			}
			else if (remainder.StartsWith("SESSION_GROUP=", StringComparison.Ordinal))
			{
				(_, string sessionGroupStr, remainder) = Tokenizer.ReadKeyValueAssignment(remainder);

				if (int.TryParse(sessionGroupStr, out int sessionGroupInt))
				{
					sessionGroup = sessionGroupInt;
				}

				continue;
			}
			else if (remainder.StartsWith("SOURCE_ADDR=", StringComparison.Ordinal))
			{
				(_, sourceAddr, remainder) = Tokenizer.ReadKeyValueAssignment(remainder);
				continue;
			}

			// Read KEY=VALUE assignments.
			(string key, string value, string rest) = Tokenizer.ReadKeyValueAssignment(remainder);

			if (key == "ISO_FIELDS")
			{
				if (value != "")
				{
					string[] flags = value.Split(',');
					isoFields = flags.Select(x => Tokenizer.ParseEnumValue(x, IsoFieldFlag.UNKNOWN)).ToList();
				}
			}
			else if (key == "PURPOSE")
			{
				purpose = Tokenizer.ParseEnumValue(value, Messages.StreamStatus.Purpose.UNKNOWN);
			}
			else if (key == "REASON")
			{
				reason = Tokenizer.ParseEnumValue(value, Messages.StreamStatus.Reason.UNKNOWN);
			}
			else if (key == "REMOTE_REASON")
			{
				reason = Tokenizer.ParseEnumValue(value, Messages.StreamStatus.Reason.UNKNOWN);
			}
			else if (key == "CLIENT_PROTOCOL")
			{
				clientProtocol = Tokenizer.ParseEnumValue(value, Messages.StreamStatus.ClientProtocol.UNKNOWN);
			}
			else
			{
				Logger.LogError($"Unknown key '{key}'.");
			}

			remainder = rest;
		}

		StreamInfo circuitInfo = new(streamId, streamStatus, circuitId, targetAddress, targetPort)
		{
			Reason = reason,
			RemoteReason = remoteReason,
			Source = source,
			SourceAddr = sourceAddr,
			Purpose = purpose,
			UserName = userName,
			UserPassword = userPassword,
			ClientProtocol = clientProtocol,
			NymEpoch = nymEpoch,
			SessionGroup = sessionGroup,
			IsoFields = isoFields,
		};

		return circuitInfo;
	}

	/// <inheritdoc/>
	public override string ToString()
	{
		string args = string.Join(
			separator: ", ",
			$"{nameof(StreamID)}={StreamID}",
			$"{nameof(StreamStatus)}={StreamStatus}",
			$"{nameof(CircuitID)}={CircuitID}",
			$"{nameof(TargetAddress)}='{TargetAddress}'",
			$"{nameof(Port)}={Port}",
			$"{nameof(Reason)}={Reason?.ToString() ?? "null"}",
			$"{nameof(RemoteReason)}={RemoteReason?.ToString() ?? "null"}",
			$"{nameof(Source)}={Source ?? "null"}",
			$"{nameof(Purpose)}={Purpose?.ToString() ?? "null"}",
			$"{nameof(UserName)}='{UserName ?? "null"}'",
			$"{nameof(UserPassword)}='{UserPassword ?? "null"}'",
			$"{nameof(ClientProtocol)}={ClientProtocol?.ToString() ?? "null"}",
			$"{nameof(NymEpoch)}={NymEpoch?.ToString() ?? "null"}",
			$"{nameof(SessionGroup)}={SessionGroup?.ToString() ?? "null"}",
			$"{nameof(IsoFields)}='{string.Join('|', IsoFields)}'"
		);

		return $"{nameof(StreamInfo)}{{{args}}}";
	}
}
