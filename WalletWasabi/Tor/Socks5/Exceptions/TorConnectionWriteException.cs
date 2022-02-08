namespace WalletWasabi.Tor.Socks5.Exceptions;

/// <summary>
/// For any failures in sending data to Tor SOCKS5 endpoint.
/// </summary>
public class TorConnectionWriteException : TorConnectionException
{
	public TorConnectionWriteException(string message) : base(message)
	{
	}

	public TorConnectionWriteException(string message, Exception innerException) : base(message, innerException)
	{
	}
}
