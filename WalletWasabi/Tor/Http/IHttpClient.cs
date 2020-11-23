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

		/// <inheritdoc cref="HttpClient.SendAsync(HttpRequestMessage, CancellationToken)"/>
		Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token = default);

		async Task<HttpResponseMessage> SendAsync(HttpMethod method, Uri requestUri, HttpContent? content = null, CancellationToken token = default)
		{
			using var httpRequestMessage = new HttpRequestMessage(method, requestUri);

			if (content is { })
			{
				httpRequestMessage.Content = content;
			}

			return await SendAsync(httpRequestMessage, token).ConfigureAwait(false);
		}
	}
}
