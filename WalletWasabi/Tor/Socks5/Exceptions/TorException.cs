namespace WalletWasabi.Tor.Socks5.Exceptions;

/// <summary>
/// A base class for exceptions thrown by the Tor SOCKS5 classes.
/// </summary>
/// <remarks>This exception is abstract because it is not supposed to be instantiated.</remarks>
public abstract class TorException : Exception
{
	public TorException() : base()
	{
	}

	public TorException(string message) : base(message)
	{
	}

	public TorException(string message, Exception innerException) : base(message, innerException)
	{
	}
}
