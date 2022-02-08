namespace WalletWasabi.Tor.Control.Exceptions;

public class TorControlException : Exception
{
	public TorControlException(string message) : base(message)
	{
	}

	public TorControlException(string message, Exception innerException) : base(message, innerException)
	{
	}
}
