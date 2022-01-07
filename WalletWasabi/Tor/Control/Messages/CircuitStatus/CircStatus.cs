namespace WalletWasabi.Tor.Control.Messages.CircuitStatus;

public enum CircStatus
{
	/// <summary>Circuit ID assigned to new circuit</summary>
	LAUNCHED,

	/// <summary>All hops finished, can now accept streams</summary>
	BUILT,

	/// <summary>All hops finished, waiting to see if a circuit with a better guard will be usable.</summary>
	GUARD_WAIT,

	/// <summary>One more hop has been completed</summary>
	EXTENDED,

	/// <summary>Circuit closed (was not built)</summary>
	FAILED,

	/// <summary>Circuit closed (was built)</summary>
	CLOSED,

	/// <summary>Reserved for unknown values</summary>
	UNKNOWN,
}
