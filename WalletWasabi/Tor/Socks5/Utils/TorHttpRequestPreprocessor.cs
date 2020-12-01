using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tor.Http.Models;

namespace WalletWasabi.Tor.Socks5.Utils
{
	public class TorHttpRequestPreprocessor
	{
		/// <summary>
		/// Pre-processes <see cref="HttpRequestMessage"/> so that the request is compliant with RFC 7230 and Wasabi Backend can accept the request (e.g. GZIP).
		/// </summary>
		/// <param name="request">HTTP request to pre-process.</param>
		/// <param name="token">Cancellation token to cancel the asynchronous operation.</param>
		/// <seealso href="https://tools.ietf.org/html/rfc7230"/>
		public static async Task PreprocessAsync(HttpRequestMessage request, CancellationToken token)
		{
			// https://tools.ietf.org/html/rfc7230#section-2.6
			// Intermediaries that process HTTP messages (i.e., all intermediaries other than those acting as tunnels) MUST send their own HTTP - version in forwarded messages.
			request.Version = HttpProtocol.HTTP11.Version;

			request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
		}
	}
}
