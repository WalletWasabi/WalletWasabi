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
	/// <param name="cancellationToken">Cancellation token to cancel the asynchronous operation.</param>
	/// <exception cref="HttpRequestException"/>
	/// <exception cref="OperationCanceledException">When operation is canceled.</exception>
	Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken);

	/// <exception cref="HttpRequestException"/>
	/// <exception cref="InvalidOperationException"/>
	/// <exception cref="OperationCanceledException">When operation is canceled.</exception>
	async Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativeUri, HttpContent? content = null, CancellationToken cancellationToken = default)
	{
		if (BaseUriGetter is null)
		{
			throw new InvalidOperationException($"{nameof(BaseUriGetter)} is not set.");
		}

		Uri baseUri = BaseUriGetter.Invoke()
			?? throw new InvalidOperationException("Base URI is not set.");
		Uri requestUri = new(baseUri, relativeUri);
		using HttpRequestMessage httpRequestMessage = new(method, requestUri);

		if (content is { })
		{
			httpRequestMessage.Content = content;
		}

		return await SendAsync(httpRequestMessage, cancellationToken).ConfigureAwait(false);
	}
}
