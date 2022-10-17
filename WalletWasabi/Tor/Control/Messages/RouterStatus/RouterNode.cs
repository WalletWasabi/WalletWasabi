using Microsoft.AspNetCore.Components.Server.Circuits;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WalletWasabi.Tor.Control.Exceptions;
using WalletWasabi.Tor.Control.Messages.CircuitStatus;
using WalletWasabi.Tor.Control.Utils;

namespace WalletWasabi.Tor.Control.Messages.RouterStatus;

/// <seealso href="https://github.com/torproject/torspec/blob/e5cd03e38e25ddb10f3138ac25ec684890f97f00/dir-spec.txt#L2288-L2465"/>
public record RouterNode(string Nickname, RouterNodeFlags[] Flags, long? Bandwidth)
{
	/// <summary>
	/// Parses response of <c>getinfo ns/all</c> command.
	/// </summary>
	/// <returns>Rounter nodes in the same order as get from the <c>getinfo ns/all</c> command.</returns>
	/// <exception cref="TorControlException">If the reply is not syntactically valid.</exception>
	public static List<RouterNode> FromReply(TorControlReply reply)
	{
		if (!reply.Success)
		{
			throw new TorControlException("Failed to get router status info entries."); // This should never happen.
		}

		string? nickname = null;
		RouterNodeFlags[]? flags = null;
		long? bandwidth = null;

		List<RouterNode> list = new();

		foreach (string line in reply.ResponseLines)
		{
			if (line == "ns/all=")
			{
				continue;
			}

			// Either last line or new "r" record.
			if (nickname is not null && (line == "." || line.StartsWith('r')))
			{
				if (flags is null)
				{
					throw new TorControlException("Unexpected state."); // This should never happen.
				}

				list.Add(new RouterNode(nickname, flags, bandwidth));

				// We do not expect to get here without visiting a new "r" line.
				nickname = null;
				flags = null;
				bandwidth = null;
			}

			// "r" record is present exactly once.
			if (line.StartsWith('r'))
			{
				string[] rValues = line.Split(' ');
				nickname = rValues[1];

				continue;
			}

			// "s" record is present exactly once.
			if (line.StartsWith('s'))
			{
				// Format of line is: "s" SP Flags NL
				string[] sValues = line.Split(' ');

				// Skip the initial "s" value and parse remaining flags.
				flags = sValues.Skip(1).Select(x => Tokenizer.ParseEnumValue(x, RouterNodeFlags.UNKNOWN)).ToArray();
			}

			// "w" record is present AT MOST once.
			if (line.StartsWith('w'))
			{
				// Format of line is: "w" SP "Bandwidth=" INT [SP "Measured=" INT] [SP "Unmeasured=1"] NL
				string[] wValues = line.Split(' ');

				(string key, string value, string _) = Tokenizer.ReadKeyValueAssignment(wValues[1]);

				if (key != "Bandwidth")
				{
					throw new TorControlException("Invalid key name."); // This should never happen.
				}

				if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long bandwidthValue))
				{
					throw new TorControlException("Bandwidth is not a number."); // This should never happen.
				}

				bandwidth = bandwidthValue;
			}
		}

		return list;
	}

	/// <inheritdoc/>
	public override string ToString()
	{
		string args = string.Join(
		separator: ", ",
			$"{nameof(Nickname)}='{Nickname}'",
			$"{nameof(Flags)}='{string.Join('|', Flags)}'",
			$"{nameof(Bandwidth)}={(Bandwidth.HasValue ? Bandwidth : "null")}");

		return $"{nameof(RouterNode)}{{{args}}}";
	}
}
