using System;

namespace WalletWasabi.Tor.Socks5.Exceptions
{
	/// <summary>
	/// Exception thrown when reading invalid or unexpected data from Tor SOCKS5 stream.
	/// </summary>
	public class TorResponseException : TorException
	{
		public TorResponseException(string message) : base(message)
		{
		}

		public TorResponseException(string message, Exception innerException) : base(message, innerException)
		{
		}
	}
}
