using System;

namespace WalletWasabi.Tor.Socks5.Exceptions
{
	public class TorException : Exception
	{
		public TorException(string message) : base(message)
		{
		}

		public TorException(string message, Exception innerException) : base(message, innerException)
		{
		}
	}
}
