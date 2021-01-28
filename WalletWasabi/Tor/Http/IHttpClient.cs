using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Tor.Http
{
	/// <summary>
	/// Interface defining HTTP client capable of sending either absolute or relative HTTP requests.
	/// </summary>
	/// <remarks>Relative HTTP requests are allowed only when <see cref="BaseUriGetter"/> returns non-<see langword="null"/> value.</remarks>
	public interface IHttpClient
	{
		Func<Uri?> BaseUriGetter { get; }

		/// <summary>
		/// Whether each HTTP(s) request should use a separate Tor circuit by default or not to increase privacy.
		/// <para>This property may be set to <c>false</c> and you can still call override the value when sending a single HTTP(s) request using <see cref="IHttpClient"/> API.</para>
		/// </summary>
		/// <remarks>The property name make sense only when talking about Tor <see cref="TorHttpClient"/>.</remarks>
		bool DefaultIsolateStream { get; }

		/// <summary>Sends an HTTP(s) request.</summary>
		/// <param name="request">HTTP request message to send.</param>
		/// <param name="isolateStream"><c>true</c> value is only available for Tor HTTP client to use a new Tor circuit
		/// for this single HTTP(s) request, otherwise <c>false</c> when no new Tor circuit is required or when <see cref="IHttpClient"/>
		/// implementation does not support this option (e.g. clearnet).</param>
		/// <param name="token">Cancellation token to cancel the asynchronous operation.</param>
		Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, bool isolateStream, CancellationToken token = default);

		Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token = default)
		{
			return SendAsync(request, DefaultIsolateStream, token);
		}

		/// <exception cref="InvalidOperationException"/>
		async Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativeUri, HttpContent? content = null, CancellationToken cancel = default)
		{
			Uri? baseUri = BaseUriGetter.Invoke();

			if (baseUri is null)
			{
				throw new InvalidOperationException("Base URI is not set.");
			}

			var requestUri = new Uri(baseUri, relativeUri);
			using var httpRequestMessage = new HttpRequestMessage(method, requestUri);

			if (content is { })
			{
				httpRequestMessage.Content = content;
			}

			return await SendAsync(httpRequestMessage, DefaultIsolateStream, cancel).ConfigureAwait(false);
		}
	}
}