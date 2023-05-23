using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Control.Utils;

namespace WalletWasabi.Tor.Control.Messages.CircuitStatus;

/// <summary>Implemented as specified in <c>4.1.1. Circuit status changed</c> spec.</summary>
/// <remarks>
/// Note that the <see cref="CircuitID"/> and <see cref="CircuitStatus"/> are then only mandatory
/// fields in <c>GETINFO circuit-status</c> reply.
/// </remarks>
public record CircuitInfo
{
	public CircuitInfo(string circuitID, CircuitStatus circuitStatus)
	{
		CircuitID = circuitID;
		CircuitStatus = circuitStatus;
	}

	/// <summary>Unique circuit identifier.</summary>
	/// <remarks>
	/// Currently, Tor only uses digits, but this may change.
	/// <para>String matches <c>^[a-zA-Z0-9]{1,16}$</c>.</para>
	/// </remarks>
	/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">2.4. General-use tokens</seealso>
	public string CircuitID { get; }
	public CircuitStatus CircuitStatus { get; }
	public List<CircuitPath> CircuitPaths { get; init; } = new();
	public List<BuildFlag> BuildFlags { get; init; } = new();
	public Purpose? Purpose { get; init; }
	public HsState? HsState { get; init; }

	/// <summary>Onion address.</summary>
	/// <remarks>
	/// This field is provided only for hidden-service-related circuits.
	/// <para>Clients MUST accept hidden service addresses in formats other than that specified above.</para>
	/// </remarks>
	public string? RendQuery { get; init; }
	public string? TimeCreated { get; init; }
	public Reason? Reason { get; init; }
	public Reason? RemoteReason { get; init; }
	public string? UserName { get; init; }
	public string? UserPassword { get; init; }

	public static CircuitInfo ParseLine(string line)
	{
		(string circuitId, string remainder1) = Tokenizer.ReadUntilSeparator(line);
		(string circuitStatusString, string remainder2) = Tokenizer.ReadUntilSeparator(remainder1);

		CircuitStatus circuitStatus = Tokenizer.ParseEnumValue(circuitStatusString, CircuitStatus.UNKNOWN);

		string remainder = remainder2;

		List<BuildFlag> buildFlags = new();
		Purpose? purpose = null;
		HsState? hsState = null;
		string? rendQuery = null;
		string? timeCreated = null;
		Reason? reason = null;
		Reason? remoteReason = null;
		string? userName = null;
		string? userPassword = null;
		List<CircuitPath> circuitPaths = new();

		// Optional arguments.
		while (remainder != "")
		{
			// Read <PATH>.
			if (remainder.StartsWith("$", StringComparison.Ordinal))
			{
				string pathVal;
				(pathVal, remainder) = Tokenizer.ReadUntilSeparator(remainder);
				circuitPaths = ParseCircuitPath(pathVal);

				continue;
			}

			if (remainder.StartsWith("SOCKS_USERNAME=", StringComparison.Ordinal))
			{
				(userName, remainder) = Tokenizer.ReadKeyQuotedValueAssignment(key: "SOCKS_USERNAME", remainder);
				continue;
			}
			else if (remainder.StartsWith("SOCKS_PASSWORD=", StringComparison.Ordinal))
			{
				(userPassword, remainder) = Tokenizer.ReadKeyQuotedValueAssignment(key: "SOCKS_PASSWORD", remainder);
				continue;
			}

			// Read KEY=VALUE assignments.
			(string key, string value, string rest) = Tokenizer.ReadKeyValueAssignment(remainder);

			if (key == "BUILD_FLAGS")
			{
				string[] flags = value.Split(',');
				buildFlags = flags.Select(x => Tokenizer.ParseEnumValue(x, BuildFlag.UNKNOWN)).ToList();
			}
			else if (key == "PURPOSE")
			{
				purpose = Tokenizer.ParseEnumValue(value, Messages.CircuitStatus.Purpose.UNKNOWN);
			}
			else if (key == "HS_STATE")
			{
				hsState = Tokenizer.ParseEnumValue(value, Messages.CircuitStatus.HsState.UNKNOWN);
			}
			else if (key == "REND_QUERY")
			{
				rendQuery = value;
			}
			else if (key == "TIME_CREATED")
			{
				timeCreated = value;
			}
			else if (key == "REASON")
			{
				reason = Tokenizer.ParseEnumValue(value, Messages.CircuitStatus.Reason.UNKNOWN);
			}
			else if (key == "REMOTE_REASON")
			{
				reason = Tokenizer.ParseEnumValue(value, Messages.CircuitStatus.Reason.UNKNOWN);
			}
			else
			{
				Logger.LogError($"Unknown key '{key}'.");
			}

			remainder = rest;
		}

		CircuitInfo circuitInfo = new(circuitId, circuitStatus)
		{
			CircuitPaths = circuitPaths,
			BuildFlags = buildFlags,
			Purpose = purpose,
			HsState = hsState,
			RendQuery = rendQuery,
			TimeCreated = timeCreated,
			Reason = reason,
			RemoteReason = remoteReason,
			UserName = userName,
			UserPassword = userPassword,
		};

		return circuitInfo;
	}

	/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">2.4. General-use tokens (see <c>LongName</c>)</seealso>
	private static List<CircuitPath> ParseCircuitPath(string paths)
	{
		List<CircuitPath> result = new();

		foreach (string path in paths.Split(','))
		{
			if (path.IndexOf('=') > -1)
			{
				string[] parts = path.Split('=', count: 2);
				result.Add(new CircuitPath(FingerPrint: parts[0], Nickname: parts[1]));
			}
			else if (path.IndexOf('~') > -1)
			{
				string[] parts = path.Split('~', count: 2);
				result.Add(new CircuitPath(FingerPrint: parts[0], Nickname: parts[1]));
			}
			else
			{
				result.Add(new CircuitPath(FingerPrint: path, Nickname: null));
			}
		}

		return result;
	}

	/// <inheritdoc/>
	public override string ToString()
	{
		string args = string.Join(
			separator: ", ",
			$"{nameof(CircuitID)}='{CircuitID}'",
			$"{nameof(CircuitStatus)}={CircuitStatus}",
			$"{nameof(RendQuery)}={RendQuery ?? "null"}",
			$"{nameof(TimeCreated)}={TimeCreated ?? "null"}",
			$"{nameof(Purpose)}={(Purpose.HasValue ? Purpose : "null")}",
			$"{nameof(HsState)}={(HsState.HasValue ? HsState : "null")}",
			$"{nameof(Reason)}={(Reason.HasValue ? Reason : "null")}",
			$"{nameof(RemoteReason)}={(RemoteReason.HasValue ? RemoteReason : "null")}",
			$"{nameof(BuildFlags)}='{string.Join('|', BuildFlags)}'",
			$"{nameof(UserName)}={UserName ?? "null"}",
			$"{nameof(UserPassword)}={UserPassword ?? "null"}");

		return $"{nameof(CircuitInfo)}{{{args}}}";
	}
}
