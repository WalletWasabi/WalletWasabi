namespace WalletWasabi.Wallets;

/// <summary>
/// A hardware wallet operation failed. The service raises these instead of the vendor protocol errors it
/// works with, so that callers can react to what happened without knowing which device is attached.
/// </summary>
public class HardwareWalletException : Exception
{
	public HardwareWalletException(string message, Exception? innerException = null) : base(message, innerException)
	{
	}
}

/// <summary>The device could not be reached: not connected, locked, or held by another program.</summary>
public class HardwareWalletNotFoundException : HardwareWalletException
{
	public HardwareWalletNotFoundException(string message, Exception? innerException = null) : base(message, innerException)
	{
	}
}

/// <summary>
/// Nothing is running that could reach the device at all, as opposed to the device itself being absent.
/// The user has to install or start it; <see cref="HardwareWalletService.BridgeDownloadUrl"/> says where.
/// </summary>
public class HardwareWalletTransportNotFoundException : HardwareWalletNotFoundException
{
	public HardwareWalletTransportNotFoundException(string message, Exception? innerException = null) : base(message, innerException)
	{
	}
}
