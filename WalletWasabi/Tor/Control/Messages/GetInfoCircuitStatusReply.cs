using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Tor.Control.Exceptions;
using WalletWasabi.Tor.Control.Messages.CircuitStatus;

namespace WalletWasabi.Tor.Control.Messages;

public record GetInfoCircuitStatusReply
{
	public GetInfoCircuitStatusReply(IList<CircuitInfo> circuits)
	{
		Circuits = circuits;
	}

	public IList<CircuitInfo> Circuits { get; }

	/// <exception cref="TorControlReplyParseException"/>
	public static GetInfoCircuitStatusReply FromReply(TorControlReply reply)
	{
		if (!reply.Success)
		{
			throw new TorControlReplyParseException("GETINFO[circuit-status]: Expected reply with OK status.");
		}

		if (reply.ResponseLines.First() != "circuit-status=")
		{
			throw new TorControlReplyParseException("GETINFO[circuit-status]: First line is invalid.");
		}

		if (reply.ResponseLines[^2] != ".")
		{
			throw new TorControlReplyParseException("GETINFO[circuit-status]: Last line must be equal to dot ('.').");
		}

		IList<CircuitInfo> circuits = new List<CircuitInfo>();

		foreach (string line in reply.ResponseLines.Skip(1).SkipLast(2))
		{
			circuits.Add(CircuitInfo.ParseLine(line));
		}

		return new GetInfoCircuitStatusReply(circuits);
	}
}
