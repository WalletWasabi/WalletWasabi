using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Tor.Http
{
	/// <summary>
	/// Interface defining HTTP client capable of sending HTTP requests that are relative to some base URI.
	/// <para>This is useful to send requests to Wasabi Backend server, for example.</para>
	/// </summary>
	public interface IRelativeHttpClient : IHttpClient
	{
		Func<Uri> DestinationUriAction { get; }

		async Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativeUri, HttpContent? content = null, CancellationToken cancel = default)
		{
			var requestUri = new Uri(DestinationUriAction.Invoke(), relativeUri);
			using var httpRequestMessage = new HttpRequestMessage(method, requestUri);
			httpRequestMessage.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));

			if (content is { })
			{
				httpRequestMessage.Content = content;
			}

			return await SendAsync(httpRequestMessage, cancel).ConfigureAwait(false);
		}
	}
}
