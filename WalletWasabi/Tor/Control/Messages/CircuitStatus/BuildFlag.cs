namespace WalletWasabi.Tor.Control.Messages.CircuitStatus;

public enum BuildFlag
{
	/// <summary>One-hop circuit, used for tunneled directory conns</summary>
	ONEHOP_TUNNEL,

	/// <summary>Internal circuit, not to be used for exiting streams</summary>
	IS_INTERNAL,

	/// <summary>This circuit must use only high-capacity nodes</summary>
	NEED_CAPACITY,

	/// <summary>This circuit must use only high-uptime nodes</summary>
	NEED_UPTIME,

	/// <summary>Reserved for unknown values</summary>
	UNKNOWN
}
