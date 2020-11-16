using System;

namespace WalletWasabi.Tor.Socks5.Exceptions
{
	/// <summary>
	/// TCP connection with Tor SOCKS5 was established but SOCKS5 authentication failed.
	/// </summary>
	public class TorAuthenticationException : TorException
	{
		public TorAuthenticationException(string message) : base(message)
		{
		}

		public TorAuthenticationException(string message, Exception innerException) : base(message, innerException)
		{
		}
	}
}
