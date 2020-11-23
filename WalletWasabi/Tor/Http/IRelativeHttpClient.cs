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

		/// <summary>
		/// Whether each HTTP(s) request should use a separate Tor circuit by default or not to increase privacy.
		/// <para>This property may be set to <c>false</c> and you can still call override the value when sending a single HTTP(s) request using <see cref="IHttpClient"/> API.</para>
		/// </summary>
		/// <remarks>The property name make sense only when talking about Tor <see cref="TorHttpClient"/>.</remarks>
		bool DefaultIsolateStream { get; }

		Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token = default)
		{
			return SendAsync(request, DefaultIsolateStream, token);
		}

		async Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativeUri, HttpContent? content = null, CancellationToken cancel = default)
		{
			var requestUri = new Uri(DestinationUriAction.Invoke(), relativeUri);
			using var httpRequestMessage = new HttpRequestMessage(method, requestUri);

			if (content is { })
			{
				httpRequestMessage.Content = content;
			}

			return await SendAsync(httpRequestMessage, DefaultIsolateStream, cancel).ConfigureAwait(false);
		}
	}
}
