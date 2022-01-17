namespace WalletWasabi.Tor.Control.Messages.Events.OrEvents;

public enum Reason
{
	/// <summary>The OR connection has shut down cleanly.</summary>
	DONE,

	/// <summary>We got an ECONNREFUSED while connecting to the target OR.</summary>
	CONNECTREFUSED,

	/// <summary>We connected to the OR, but found that its identity was not what we expected.</summary>
	IDENTITY,

	/// <summary>We got an ECONNRESET or similar IO error from the connection with the OR.</summary>
	CONNECTRESET,

	/// <summary>
	/// We got an ETIMEOUT or similar IO error from the connection with the OR,
	/// or we're closing the connection for being idle for too long.
	/// </summary>
	TIMEOUT,

	/// <summary>We got an ENOTCONN, ENETUNREACH, ENETDOWN, EHOSTUNREACH, or similar error while connecting to the OR.</summary>
	NOROUTE,

	/// <summary>We got some other IO error on our connection to the OR.</summary>
	IOERROR,

	/// <summary>We don't have enough operating system resources (file descriptors, buffers, etc) to connect to the OR.</summary>
	RESOURCELIMIT,

	/// <summary>No pluggable transport was available.</summary>
	PT_MISSING,

	/// <summary>The OR connection closed for some other reason.</summary>
	MISC,

	/// <summary>Reserved for unknown values.</summary>
	UNKNOWN
}
