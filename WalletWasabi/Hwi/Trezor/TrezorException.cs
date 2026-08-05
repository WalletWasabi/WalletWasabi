namespace WalletWasabi.Hwi.Trezor;

public class TrezorException : Exception
{
	public TrezorException(string message) : base(message)
	{
	}
}

/// <summary>
/// The device (or its bridge) was not available, as opposed to the device refusing an operation.
/// Lets the UI tell the user to connect and unlock the Trezor instead of reporting a declined action.
/// </summary>
public class TrezorDeviceNotFoundException : TrezorException
{
	public TrezorDeviceNotFoundException(string message) : base(message)
	{
	}
}

/// <summary>
/// No Trezor Bridge is reachable at all (neither Trezor Suite nor trezord is running and none could be
/// started), as opposed to a running bridge seeing no device. Lets the UI point the user at installing
/// the bridge instead of telling them to connect the device.
/// </summary>
public class TrezorBridgeNotFoundException : TrezorDeviceNotFoundException
{
	public TrezorBridgeNotFoundException(string message) : base(message)
	{
	}
}
