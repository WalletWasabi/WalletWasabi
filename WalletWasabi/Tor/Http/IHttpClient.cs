using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Tor.Http;

/// <summary>
/// Interface defining HTTP client capable of sending either absolute or relative HTTP requests.
/// </summary>
/// <remarks>Relative HTTP requests are allowed only when <see cref="BaseUriGetter"/> returns non-<see langword="null"/> value.</remarks>
public interface IHttpClient
{
	Func<Uri>? BaseUriGetter { get; }

	/// <summary>Sends an HTTP(s) request.</summary>
	/// <param name="request">HTTP request message to send.</param>
	/// <param name="token">Cancellation token to cancel the asynchronous operation.</param>
	/// <exception cref="HttpRequestException"/>
	Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token = default);

	/// <exception cref="HttpRequestException"/>
	/// <exception cref="InvalidOperationException"/>
	async Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativeUri, HttpContent? content = null, CancellationToken cancel = default)
	{
		if (BaseUriGetter is null)
		{
			throw new InvalidOperationException($"{nameof(BaseUriGetter)} is not set.");
		}

		Uri? baseUri = BaseUriGetter.Invoke();

		if (baseUri is null)
		{
			throw new InvalidOperationException("Base URI is not set.");
		}

		Uri requestUri = new(baseUri, relativeUri);
		using HttpRequestMessage httpRequestMessage = new(method, requestUri);

		if (content is { })
		{
			httpRequestMessage.Content = content;
		}

		return await SendAsync(httpRequestMessage, cancel).ConfigureAwait(false);
	}
}
