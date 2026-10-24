namespace WalletWasabi.Wallets;

/// <summary>Base of every error a hardware wallet operation raises; vendor exceptions derive from it, so callers need not know which device is attached.</summary>
public class HardwareWalletException : Exception
{
	public HardwareWalletException(string message) : base(message)
	{
	}
}

/// <summary>The device could not be reached: not connected, locked, or held by another program.</summary>
public class HardwareWalletNotFoundException : HardwareWalletException
{
	public HardwareWalletNotFoundException(string message) : base(message)
	{
	}
}

/// <summary>Nothing is running that could reach the device at all; the user has to install or start it (<see cref="HardwareWalletService.BridgeDownloadUrl"/> says where).</summary>
public class HardwareWalletTransportNotFoundException : HardwareWalletNotFoundException
{
	public HardwareWalletTransportNotFoundException(string message) : base(message)
	{
	}
}
