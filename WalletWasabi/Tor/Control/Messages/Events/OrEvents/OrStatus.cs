namespace WalletWasabi.Tor.Control.Messages.Events.OrEvents;

public enum OrStatus
{
	/// <summary>We have received a new incoming OR connection, and are starting the server-side handshake.</summary>
	NEW,

	/// <summary>We have launched a new outgoing OR connection, and are starting the client-side handshake.</summary>
	LAUNCHED,

	/// <summary>The OR connection has been connected and the handshake is done.</summary>
	CONNECTED,

	/// <summary>Our attempt to open the OR connection failed.</summary>
	FAILED,

	/// <summary>The OR connection closed in an unremarkable way.</summary>
	CLOSED,

	/// <summary>Reserved for unknown values.</summary>
	UNKNOWN
}
