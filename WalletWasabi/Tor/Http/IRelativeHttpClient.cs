using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Tor.Http
{
	/// <summary>
	/// Interface defining HTTP client capable of sending HTTP requests that are relative to some base URI.
	/// <para>This is useful, for example, to send requests to Wasabi Backend server.</para>
	/// </summary>
	public interface IRelativeHttpClient : IHttpClient
	{
		Func<Uri> DestinationUriAction { get; }

		async Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativeUri, HttpContent? content = null, CancellationToken cancel = default)
		{
			var requestUri = new Uri(DestinationUriAction.Invoke(), relativeUri);
			using var httpRequestMessage = new HttpRequestMessage(method, requestUri);

			if (content is { })
			{
				httpRequestMessage.Content = content;
			}

			return await SendAsync(httpRequestMessage, cancel).ConfigureAwait(false);
		}
	}
}
