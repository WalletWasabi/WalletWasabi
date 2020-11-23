using System;
using System.Net.Http;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Tor.Http
{
	/// <summary>
	/// Interface defining HTTP client capable of sending HTTP requests.
	/// </summary>
	public interface IHttpClient
	{
		/// <summary>TLS protocols we support for both clearnet and Tor proxy.</summary>
		static readonly SslProtocols SupportedSslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12;

		/// <summary>Sends an HTTP(s) request.</summary>
		/// <param name="request">HTTP request message to send.</param>
		/// <param name="isolateStream"><c>true</c> value is only available for Tor HTTP client to use a new Tor circuit
		/// for this single HTTP(s) request, otherwise <c>false</c> when no new Tor circuit is required or when <see cref="IHttpClient"/>
		/// implementation does not support this option (e.g. clearnet).</param>
		/// <param name="token">Cancellation token to cancel the asynchronous operation.</param>
		Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, bool isolateStream, CancellationToken token = default);
	}
}
