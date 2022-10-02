using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Backend.Statistics;

namespace WalletWasabi.Tor.Http;

/// <summary>
/// Interface defining HTTP client capable of sending either absolute or relative HTTP requests.
/// </summary>
/// <remarks>Relative HTTP requests are allowed only when <see cref="BaseUriGetter"/> returns non-<see langword="null"/> value.</remarks>
public interface IHttpClient
{
	Func<Uri>? BaseUriGetter { get; }

	/// <inheritdoc cref="SendAsync(HttpRequestMessage, StatsLogger, CancellationToken)"/>
	Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
	{
		return SendAsync(request, statsLogger: null, cancellationToken);
	}

	/// <summary>
	/// Sends an HTTP(s) request.
	/// </summary>
	/// <param name="request">HTTP request message to send.</param>
	/// <param name="statsLogger">Stats logger.</param>
	/// <param name="cancellationToken">Cancellation token to cancel the asynchronous operation.</param>
	/// <exception cref="HttpRequestException"/>
	/// <exception cref="OperationCanceledException">When operation is canceled.</exception>
	Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, StatsLogger? statsLogger, CancellationToken cancellationToken = default);

	Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativeUri, HttpContent? content = null, CancellationToken cancellationToken = default)
	{
		return SendAsync(method, relativeUri, content, statsLogger: null, cancellationToken);
	}

	/// <exception cref="HttpRequestException"/>
	/// <exception cref="InvalidOperationException"/>
	/// <exception cref="OperationCanceledException">When operation is canceled.</exception>
	async Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativeUri, HttpContent? content, StatsLogger? statsLogger, CancellationToken cancellationToken)
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

		return await SendAsync(httpRequestMessage, statsLogger, cancellationToken).ConfigureAwait(false);
	}
}
