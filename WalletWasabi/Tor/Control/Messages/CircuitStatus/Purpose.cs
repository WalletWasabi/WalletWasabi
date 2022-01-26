namespace WalletWasabi.Tor.Control.Messages.CircuitStatus;

public enum Purpose
{
	/// <summary>Circuit for AP and/or directory request streams</summary>
	GENERAL,

	/// <summary>HS client-side introduction-point circuit</summary>
	HS_CLIENT_INTRO,

	/// <summary>HS client-side rendezvous circuit; carries AP streams</summary>
	HS_CLIENT_REND,

	/// <summary>Circuit is used for getting HS directories</summary>
	HS_CLIENT_HSDIR,

	/// <summary>HS service-side introduction-point circuit</summary>
	HS_SERVICE_INTRO,

	/// <summary>HS service-side rendezvous circuit</summary>
	HS_SERVICE_REND,

	/// <summary>Reachability-testing circuit; carries no traffic</summary>
	TESTING,

	/// <summary>Circuit built by a controller</summary>
	CONTROLLER,

	/// <summary>Circuit being kept around to see how long it takes</summary>
	MEASURE_TIMEOUT,

	/// <summary>Circuit created ahead of time when using HS vanguards, and later repurposed as needed</summary>
	HS_VANGUARDS,

	/// <summary>Circuit used to probe whether our circuits are being deliberately closed by an attacker</summary>
	PATH_BIAS_TESTING,

	/// <summary>Circuit that is being held open to disguise its true close time</summary>
	CIRCUIT_PADDING,

	/// <summary>Reserved for unknown values</summary>
	UNKNOWN
}
