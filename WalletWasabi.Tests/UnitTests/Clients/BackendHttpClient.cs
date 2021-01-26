using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tor.Http;

namespace WalletWasabi.Tests.UnitTests.Clients
{
	/// <summary>
	/// Convenience class that allows sending HTTP request to Wasabi Backend using relative URIs.
	/// </summary>
	public class BackendHttpClient
	{
		/// <summary>
		/// Creates a new instance of the object.
		/// </summary>
		/// <param name="httpClient">Use <see cref="ClearnetHttpClient"/> or <see cref="TorHttpClient"/> to initialize.</param>
		public BackendHttpClient(IRelativeHttpClient httpClient)
		{
			HttpClient = httpClient;
		}

		private IRelativeHttpClient HttpClient { get; }

		/// <summary>
		/// Sends an HTTP request to Wasabi Backend.
		/// </summary>
		/// <param name="method">HTTP method</param>
		/// <param name="apiQuery">API query part to append to <see cref="IRelativeHttpClient.DestinationUriAction"/>.</param>
		/// <param name="content">HTTP request content. Only for <see cref="HttpMethod.Post"/> method.</param>
		/// <param name="token">Cancellation token to cancel the asynchronous operation.</param>
		public Task<HttpResponseMessage> SendAsync(HttpMethod method, string apiQuery, HttpContent? content = null, CancellationToken token = default)
		{
			if (apiQuery.StartsWith("/"))
			{
				throw new ArgumentException("Forward slash would lead to http://backend:port/<relativeUri> URI instead of http://backend:port/api/v<version>/<relativeUri>.", nameof(apiQuery));
			}

			return HttpClient.SendAsync(method, apiQuery, content, token);
		}
	}
}
