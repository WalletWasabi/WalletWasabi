using System;

namespace WalletWasabi.Tor.Socks5.Exceptions
{
	/// <summary>
	/// Thrown when SOCKS5 authentication fails for any reason.
	/// </summary>
	/// <remarks>For example, invalid credentials were provided or we do not support some SOCKS5 authentication method.</remarks>
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
