using System.Net.Http;
using System.Net.Http.Headers;
using WalletWasabi.Tor.Http.Models;

namespace WalletWasabi.Tor.Socks5.Utils
{
	public class TorHttpRequestPreprocessor
	{
		/// <summary>
		/// Pre-processes <see cref="HttpRequestMessage"/> so that the HTTP request is compliant with what Wasabi Backend accepts (e.g. GZIP).
		/// </summary>
		/// <param name="request">HTTP request to pre-process.</param>
		/// <seealso href="https://tools.ietf.org/html/rfc7230"/>
		public static void Preprocess(HttpRequestMessage request)
		{
			// https://tools.ietf.org/html/rfc7230#section-2.6
			// Intermediaries that process HTTP messages (i.e., all intermediaries other than those acting as tunnels) MUST send their own HTTP - version in forwarded messages.
			request.Version = HttpProtocol.HTTP11.Version;

			request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
		}
	}
}
