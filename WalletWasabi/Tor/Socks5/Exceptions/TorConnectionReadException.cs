namespace WalletWasabi.Tor.Socks5.Exceptions;

/// <summary>
/// For any failures in receiving data from Tor SOCKS5 endpoint.
/// </summary>
/// <remarks>This covers scenarios like TCP connection dies, HTTP response cannot be parsed, etc.</remarks>
public class TorConnectionReadException : TorConnectionException
{
	public TorConnectionReadException(string message) : base(message)
	{
	}

	public TorConnectionReadException(string message, Exception innerException) : base(message, innerException)
	{
	}
}
