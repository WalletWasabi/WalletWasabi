using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tor.Http;

namespace WalletWasabi.Tests.Helpers;

/// <summary>
/// HTTP client that allows us to do <see cref="https://en.wikipedia.org/wiki/Monkey_testing">monkey testing</see>.
/// </summary>
public class MonkeyHttpClient : IHttpClient
{
	private readonly Monkey[] _monkeys;

	public MonkeyHttpClient(IHttpClient httpClient, params Monkey[] monkeys)
	{
		HttpClient = httpClient;
		_monkeys = monkeys;
	}

	/// <summary>Monkey callback that tampers with sending of HTTP requests.</summary>
	/// <remarks>Callback can delay HTTP request processing, it can throw an exception, it can employ randomization, etc.</remarks>
	public delegate Task Monkey();

	private IHttpClient HttpClient { get; }

	public Func<Uri>? BaseUriGetter => HttpClient.BaseUriGetter;

	public virtual async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token = default)
	{
		foreach (Monkey monkey in _monkeys)
		{
			await monkey().ConfigureAwait(false);
		}

		return await HttpClient.SendAsync(request, token).ConfigureAwait(false);
	}
}
