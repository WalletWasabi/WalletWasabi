namespace WalletWasabi.Wallets;

/// <summary>
/// Where a wallet stands in asking its signing device to authorize a batch of coinjoin rounds.
/// Reported by the backend so every front end (GUI, daemon, JSON-RPC) tells the same story.
/// </summary>
public enum DeviceAuthorizationStatus
{
	/// <summary>Nothing has been asked of the device.</summary>
	Idle,

	/// <summary>The device is waiting for the user to confirm on its screen.</summary>
	AwaitingConfirmation,

	/// <summary>The device authorized the rounds; coinjoin can start.</summary>
	Confirmed,

	/// <summary>Nothing can reach a device at all, so there is nothing to confirm on.</summary>
	TransportNotFound,

	/// <summary>A transport exists but no matching device answered on it.</summary>
	DeviceNotFound,

	/// <summary>The device was reached and did not authorize.</summary>
	Failed,
}
