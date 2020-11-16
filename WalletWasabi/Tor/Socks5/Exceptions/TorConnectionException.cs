using System;

namespace WalletWasabi.Tor.Socks5.Exceptions
{
	/// <summary>
	/// TCP connection to Tor SOCKS5 endpoint failed.
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
}
