using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Tor.Http
{
	/// <summary>
	/// HTTP client implementation based on .NET's <see cref="HttpClient"/> which provides least privacy for Wasabi users,
	/// as HTTP requests are being sent over clearnet.
	/// </summary>
	/// <remarks>Inner <see cref="HttpClient"/> instance is thread-safe.</remarks>
	public class ClearnetHttpClient : IRelativeHttpClient
	{
		static ClearnetHttpClient()
		{
			HttpClient = new HttpClient(new HttpClientHandler()
			{
				AutomaticDecompression = DecompressionMethods.GZip,
				SslProtocols = IHttpClient.SupportedSslProtocols
			});
		}

		public ClearnetHttpClient(Func<Uri> destinationUriAction)
		{
			DestinationUriAction = destinationUriAction;
		}

		public Func<Uri> DestinationUriAction { get; }

		/// <summary>Predefined HTTP client that handles HTTP requests when Tor is disabled.</summary>
		private static HttpClient HttpClient { get; }

		/// <summary>The value is not used at the moment.</summary>
		/// <remarks>
		/// There is currently no mechanism to make HTTP(s) requests "more private" when using clearnet (i.e. .NET's <see cref="HttpClient"/>).
		/// <para>If you need privacy, use Tor.</para>
		/// </remarks>
		public bool DefaultIsolateStream => false;

		/// <inheritdoc cref="HttpClient.SendAsync(HttpRequestMessage, CancellationToken)"/>
		public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token = default)
		{
			return SendAsync(request, isolateStream: false, token);
		}

		/// <param name="isolateStream">Clearnet HTTP client does not support this option.</param>
		/// <inheritdoc/>
		public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, bool isolateStream = false, CancellationToken token = default)
		{
			return HttpClient.SendAsync(request, token);
		}
	}
}
