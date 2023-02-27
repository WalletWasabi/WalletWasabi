namespace WalletWasabi.Tor.Control.Messages.StreamStatus;

/// <summary>
///
/// </summary>
public enum StreamStatusFlag
{
	/// <summary>New request to connect.</summary>
	NEW,

	/// <summary>New request to resolve an address.</summary>
	NEWRESOLVE,

	/// <summary>Address re-mapped to another.</summary>
	REMAP,

	/// <summary>Sent a connect cell along a circuit.</summary>
	SENTCONNECT,

	/// <summary>Sent a resolve cell along a circuit.</summary>
	SENTRESOLVE,

	/// <summary>Received a reply; stream established.</summary>
	SUCCEEDED,

	/// <summary>Stream failed and not retriable.</summary>
	FAILED,

	/// <summary>Stream closed.</summary>
	CLOSED,

	/// <summary>Detached from circuit; still retriable.</summary>
	DETACHED,

	/// <summary>Waiting for controller to use ATTACHSTREAM.</summary>
	/// <remarks>New in 0.4.5.1-alpha.</remarks>
	CONTROLLER_WAIT,

	/// <summary>XOFF has been sent for this stream.</summary>
	/// <remarks>New in 0.4.7.5-alpha.</remarks>
	XOFF_SENT,

	/// <summary>XOFF has been received for this stream.</summary>
	/// <remarks>New in 0.4.7.5-alpha.</remarks>
	XOFF_RECV,

	/// <summary>XON has been sent for this stream.</summary>
	/// <remarks>New in 0.4.7.5-alpha.</remarks>
	XON_SENT,

	/// <summary>XON has been received for this stream.</summary>
	/// <remarks>New in 0.4.7.5-alpha.</remarks>
	XON_RECV,

	/// <summary>Reserved for unknown values.</summary>
	UNKNOWN,
}
