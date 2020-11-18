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
			var socketHandler = new SocketsHttpHandler()
			{
				AutomaticDecompression = DecompressionMethods.All,
				PooledConnectionLifetime = TimeSpan.FromMinutes(5)
			};

			socketHandler.SslOptions.EnabledSslProtocols = IHttpClient.SupportedSslProtocols;

			HttpClient = new HttpClient(socketHandler);
		}

		public ClearnetHttpClient(Func<Uri> destinationUriAction)
		{
			DestinationUriAction = destinationUriAction;
		}

		public Func<Uri> DestinationUriAction { get; }

		/// <summary>Predefined HTTP client that handles HTTP requests when Tor is disabled.</summary>
		private static HttpClient HttpClient { get; }

		/// <inheritdoc cref="HttpClient.SendAsync(HttpRequestMessage, CancellationToken)"/>
		public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token = default)
		{
			return HttpClient.SendAsync(request, token);
		}
	}
}
