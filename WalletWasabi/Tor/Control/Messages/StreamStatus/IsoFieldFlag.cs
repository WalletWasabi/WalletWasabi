namespace WalletWasabi.Tor.Control.Messages.StreamStatus;

/// <summary>
/// The "ISO_FIELDS" field indicates the set of STREAM event fields for which stream
/// isolation is enabled for the listener port that a client used to initiate this stream.
/// </summary>
/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">See section "4.1.2. Stream status changed".</seealso>
public enum IsoFieldFlag
{
	SOCKS_USERNAME,
	SOCKS_PASSWORD,
	SESSION_GROUP,
	CLIENT_PROTOCOL,
	NYM_EPOCH,

	// Special values follow.
	CLIENTADDR,

	CLIENTPORT,
	DESTADDR,
	DESTPORT,

	/// <summary>Reserved for unknown values.</summary>
	UNKNOWN,
}
