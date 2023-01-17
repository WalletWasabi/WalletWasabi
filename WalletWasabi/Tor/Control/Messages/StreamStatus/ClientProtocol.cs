namespace WalletWasabi.Tor.Control.Messages.StreamStatus;

/// <remarks>
/// The <see cref="ClientProtocol"/> indicates the protocol that was used by a client to initiate a stream.
/// <para>
/// Streams for clients connected with different protocols are isolated on separate circuits if the
/// <c>IsolateClientProtocol</c> flag is active.
/// </para>
/// <para>Controllers MUST tolerate unrecognized client protocols.</para>
/// </remarks>
/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">See section "4.1.2. Stream status changed".</seealso>
public enum ClientProtocol
{
	/// <summary>SOCKS version 4.</summary>
	SOCKS4,

	/// <summary>SOCKS version 5.</summary>
	SOCKS5,

	TRANS,
	NATD,
	DNS,
	HTTPCONNECT,

	/// <summary>Reserved for unknown values.</summary>
	UNKNOWN,
}
