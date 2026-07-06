namespace WalletWasabi.Hwi.Trezor;

public class TrezorException : Exception
{
	public TrezorException(string message) : base(message)
	{
	}
}
