namespace WalletWasabi.Tor.Control.Messages.CircuitStatus;

public enum HsState
{
	// Client-side introduction-point circuit states

	/// <summary>Connecting to intro point.</summary>
	HSCI_CONNECTING,

	/// <summary>Sent INTRODUCE1; waiting for reply from IP (introduction point).</summary>
	HSCI_INTRO_SENT,

	/// <summary>Received reply from IP relay; closing.</summary>
	HSCI_DONE,

	// Client-side rendezvous-point circuit states

	/// <summary>Connecting to or waiting for reply from RP (rendezvous-point).</summary>
	HSCR_CONNECTING,

	/// <summary>Established RP; waiting for introduction.</summary>
	HSCR_ESTABLISHED_IDLE,

	/// <summary>Introduction sent to HS; waiting for rend.</summary>
	HSCR_ESTABLISHED_WAITING,

	/// <summary>Connected to HS.</summary>
	HSCR_JOINED,

	// Service-side introduction-point circuit states

	/// <summary>Connecting to intro point.</summary>
	HSSI_CONNECTING,

	/// <summary>Established intro point.</summary>
	HSSI_ESTABLISHED,

	// Service-side rendezvous-point circuit states

	/// <summary>Connecting to client's rend point.</summary>
	HSSR_CONNECTING,

	/// <summary>Connected to client's RP circuit.</summary>
	HSSR_JOINED,

	/// <summary>Reserved for unknown values.</summary>
	UNKNOWN,
}
