using System;

namespace WalletWasabi.Tor.Socks5.Exceptions
{
	public class TorConnectionException : Exception
	{
		public TorConnectionException(string message) : base(message)
		{
		}

		public TorConnectionException(string message, Exception innerException) : base(message, innerException)
		{
		}
	}
}
