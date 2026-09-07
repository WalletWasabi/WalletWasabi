namespace WalletWasabi.Hwi.Trezor;

/// <summary>
/// The device or its bridge failed or refused an operation. A device or bridge that cannot be reached at all
/// is reported with the <see cref="HardwareWalletNotFoundException"/> family instead.
/// </summary>
public class TrezorException : HardwareWalletException
{
	public TrezorException(string message) : base(message)
	{
	}
}
