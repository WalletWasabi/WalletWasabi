using System;
using System.Net.Http;
using WalletWasabi.Tor.Http;

namespace WalletWasabi.Tor.Socks5.Exceptions
{
	/// <summary>
	/// A base class for exceptions thrown by the Tor SOCKS5 classes.
	/// </summary>
	/// <remarks>
	/// <see cref="TorException"/> inherits <see cref="HttpRequestException"/> so that <see cref="ClearnetHttpClient"/>,
	/// <see cref="TorHttpClient"/> and possibly other implementations of <see cref="IHttpClient"/> throws an exception with common ancestor.
	/// <para>This exception is abstract because it is not supposed to be instantiated.</para>
	/// </remarks>
	public abstract class TorException : HttpRequestException
	{
		public TorException(string message) : base(message)
		{
		}

		public TorException(string message, Exception innerException) : base(message, innerException)
		{
		}
	}
}
