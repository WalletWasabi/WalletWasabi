namespace WalletWasabi.Wallets;

/// <summary>How a hardware wallet is currently reachable.</summary>
public enum HardwareWalletTransport
{
	/// <summary>No bridge involved: the device is reached over direct USB.</summary>
	DirectUsb,

	/// <summary>A bridge started and owned by Wasabi is serving the device.</summary>
	BridgeStartedByWasabi,

	/// <summary>A bridge Wasabi did not start (e.g. one from the vendor's own software) is serving the device.</summary>
	ExternalBridge,
}
