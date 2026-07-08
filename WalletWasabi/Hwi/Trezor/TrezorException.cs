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
