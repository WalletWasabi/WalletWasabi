using System;

namespace WalletWasabi.Tor.Socks5.Exceptions
{
	public class ConnectionException : Exception
	{
		public ConnectionException(string message) : base(message)
		{
		}

		public ConnectionException(string message, Exception innerException) : base(message, innerException)
		{
		}
	}
}
