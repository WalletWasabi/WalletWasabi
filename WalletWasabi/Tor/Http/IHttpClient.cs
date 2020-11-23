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

		Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, bool isolateStream, CancellationToken token = default);
	}
}
