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
	public class ClearnetHttpClient : IHttpClient
	{
		/// <summary>This field is temporary and should be ultimately removed.</summary>
		public static ClearnetHttpClient Instance = new ClearnetHttpClient();

		private ClearnetHttpClient()
		{
			HttpClient = new HttpClient(new HttpClientHandler()
			{
				AutomaticDecompression = DecompressionMethods.GZip,
				SslProtocols = IHttpClient.SupportedSslProtocols
			});
		}

		/// <summary>Predefined HTTP client that handles HTTP requests when Tor is disabled.</summary>
		private HttpClient HttpClient { get; }

		/// <inheritdoc cref="HttpClient.SendAsync(HttpRequestMessage, CancellationToken)"/>
		public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token = default)
		{
			return await HttpClient.SendAsync(request, token).ConfigureAwait(false);
		}
	}
}
