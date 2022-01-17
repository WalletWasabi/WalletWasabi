namespace WalletWasabi.Tor.Socks5.Exceptions;

/// <summary>
/// Exception for the following cases:
/// <list type="bullet">
/// <item>An error in establishing a TCP connection to Tor SOCKS5 endpoint.</item>
/// <item>Any failure in sending data to Tor SOCKS5 endpoint.</item>
/// <item>Any failure in reading data from Tor SOCKS5 endpoint.</item>
/// </list>
/// </summary>
public class TorConnectionException : TorException
{
	public TorConnectionException(string message) : base(message)
	{
	}

	public TorConnectionException(string message, Exception innerException) : base(message, innerException)
	{
	}
}
