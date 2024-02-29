using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Tor.Http;

/// <summary>
/// HTTP client implementation based on .NET's <see cref="HttpClient"/> which provides least privacy for Wasabi users,
/// as HTTP requests are being sent over clearnet.
/// </summary>
/// <remarks>Inner <see cref="HttpClient"/> instance is thread-safe.</remarks>
public class ClearnetHttpClient : IHttpClient
{
	public ClearnetHttpClient(HttpClient httpClient)
	{
		BaseUriGetter = () => httpClient.BaseAddress ?? throw new NotSupportedException("No base address was set.");
		HttpClient = httpClient;
	}

	public ClearnetHttpClient(HttpClient httpClient, Func<Uri>? baseUriGetter)
	{
		BaseUriGetter = baseUriGetter;
		HttpClient = httpClient;
	}

	public Func<Uri>? BaseUriGetter { get; }

	/// <summary>Predefined HTTP client that handles HTTP requests when Tor is disabled.</summary>
	private HttpClient HttpClient { get; }

	/// <inheritdoc cref="HttpClient.SendAsync(HttpRequestMessage, CancellationToken)"/>
	public virtual Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		return HttpClient.SendAsync(request, cancellationToken);
	}
}
