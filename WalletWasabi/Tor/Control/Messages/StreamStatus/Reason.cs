namespace WalletWasabi.Tor.Control.Messages.StreamStatus;

/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/tor-spec.txt">See section "6.3. Closing streams".</seealso>
public enum Reason
{
	/// <summary>Catch-all for unlisted reasons.</summary>
	MISC = 1,

	/// <summary>Couldn't look up hostname.</summary>
	RESOLVEFAILED = 2,

	/// <summary>Remote host refused connection.</summary>
	CONNECTREFUSED = 3,

	/// <summary>OR refuses to connect to host or port.</summary>
	EXITPOLICY = 4,

	/// <summary>Circuit is being destroyed.</summary>
	DESTROY = 5,

	/// <summary>Anonymized TCP connection was closed.</summary>
	DONE = 6,

	/// <summary>Connection timed out, or OR timed out while connecting.</summary>
	TIMEOUT = 7,

	/// <summary>Routing error while attempting to contact destination.</summary>
	NOROUTE = 8,

	/// <summary>OR is temporarily hibernating.</summary>
	HIBERNATING = 9,

	/// <summary>Internal error at the OR.</summary>
	INTERNAL = 10,

	/// <summary>OR has no resources to fulfill request.</summary>
	RESOURCELIMIT = 11,

	/// <summary>Connection was unexpectedly reset.</summary>
	CONNRESET = 12,

	/// <summary>Sent when closing connection because of Tor protocol violations.</summary>
	TORPROTOCOL = 13,

	/// <summary>Client sent RELAY_BEGIN_DIR to a non-directory relay.</summary>
	NOTDIRECTORY = 14,

	/// <summary>Reserved for unknown values.</summary>
	UNKNOWN,
}
