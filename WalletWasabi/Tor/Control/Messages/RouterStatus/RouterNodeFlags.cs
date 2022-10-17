namespace WalletWasabi.Tor.Control.Messages.RouterStatus;

/// <seealso href="https://github.com/torproject/torspec/blob/e5cd03e38e25ddb10f3138ac25ec684890f97f00/dir-spec.txt#L2327-L2365"/>
public enum RouterNodeFlags
{
	/// <summary>If the router is a directory authority.</summary>
	Authority,

	/// <summary>
	/// If the router is believed to be useless as an exit node
	/// (because its ISP censors it, because it is behind a restrictive
	/// proxy, or for some similar reason).
	/// </summary>
	BadExit,

	/// <summary>
	/// If the router is more useful for building general-purpose exit circuits
	/// than for relay circuits. The path building algorithm uses this flag; see path-spec.txt.
	/// </summary>
	Exit,

	/// <summary>If the router is suitable for high-bandwidth circuits.</summary>
	Fast,

	/// <summary>If the router is suitable for use as an entry guard.</summary>
	Guard,

	/// <summary>If the router is considered a v2 hidden service directory.</summary>
	HSDir,

	/// <summary>
	/// If the router is considered unsuitable for usage other than as a middle relay.
	/// Clients do not need to handle this option, since when it is present, the authorities
	/// will automatically vote against flags that would make the router usable in other positions.
	/// </summary>
	MiddleOnly,

	/// <summary>
	/// If any Ed25519 key in the router's descriptor or microdescriptor does not reflect authority consensus.
	/// </summary>
	NoEdConsensus,

	/// <summary>If the router is suitable for long-lived circuits.</summary>
	Stable,

	/// <summary>If the router should upload a new descriptor because the old one is too old.</summary>
	StaleDesc,

	/// <summary>
	/// If the router is currently usable over all its published ORPorts.
	/// (Authorities ignore IPv6 ORPorts unless configured to check IPv6 reachability.)
	/// Relays without this flag are omitted from the consensus, and current clients (since 0.2.9.4-alpha)
	/// assume that every listed relay has this flag.
	/// </summary>
	Running,

	/// <summary>
	/// If the router has been 'validated'. Relays without this flag are omitted
	/// from the consensus, and current clients assume that every listed relay has this flag.
	/// </summary>
	Valid,

	/// <summary>If the router implements the v2 directory protocol or higher.</summary>
	V2Dir,

	/// <summary>Reserved for unknown values.</summary>
	UNKNOWN
}
