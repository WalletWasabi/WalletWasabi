namespace WalletWasabi.Tor.Control.Messages.StreamStatus;

/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">See section "4.1.2. Stream status changed".</seealso>
public enum Purpose
{
	/// <summary>This stream is generated internally to Tor for fetching directory information.</summary>
	DIR_FETCH,

	/// <summary>An internal stream for uploading information to a directory authority.</summary>
	DIR_UPLOAD,

	/// <summary>A stream we're using to test our own directory port to make sure it's reachable.</summary>
	DIRPORT_TEST,

	/// <summary>A user-initiated DNS request.</summary>
	DNS_REQUEST,

	/// <summary>This stream is handling user traffic, OR it's internal to Tor, but it doesn't match one of the purposes above.</summary>
	USER,

	/// <summary>Reserved for unknown values.</summary>
	UNKNOWN,
}
