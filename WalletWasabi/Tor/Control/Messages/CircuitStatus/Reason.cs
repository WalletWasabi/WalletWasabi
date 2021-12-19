namespace WalletWasabi.Tor.Control.Messages.CircuitStatus;

public enum Reason
{
	/// <summary>No reason given.</summary>
	NONE,

	/// <summary>Tor protocol violation.</summary>
	TORPROTOCOL,

	/// <summary>Internal error.</summary>
	INTERNAL,

	/// <summary>Client sent a TRUNCATE command.</summary>
	REQUESTED,

	/// <summary>Relay suspended, trying to save bandwidth.</summary>
	HIBERNATING,

	/// <summary>Out of memory, sockets, or circuit IDs.</summary>
	RESOURCELIMIT,

	/// <summary>Unable to reach relay.</summary>
	CONNECTFAILED,

	/// <summary>Connected, but its OR identity was not as expected.</summary>
	OR_IDENTITY,

	/// <remarks>Renamed to <see cref="CHANNEL_CLOSED"/>.</remarks>
	OR_CONN_CLOSED,

	/// <summary>Connection that was carrying this circuit died.</summary>
	CHANNEL_CLOSED,

	/// <summary>Circuit has expired for being dirty or old.</summary>
	FINISHED,

	/// <summary>Circuit construction took too long.</summary>
	TIMEOUT,

	/// <summary>Circuit was destroyed without a client TRUNCATE.</summary>
	DESTROYED,

	/// <summary>Not enough nodes to make circuit.</summary>
	NOPATH,

	/// <summary>Request was for an unknown hidden service.</summary>
	NOSUCHSERVICE,

	/// <summary>As <see cref="TIMEOUT"/>, except that we had left the circuit open for measurement purposes to see how long it would take to finish.</summary>
	MEASUREMENT_EXPIRED,

	/// <summary>Reserved for unknown values.</summary>
	UNKNOWN
}
