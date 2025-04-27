using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Tests.Helpers;

/// <summary>
/// HTTP client that allows us to do <see cref="https://en.wikipedia.org/wiki/Monkey_testing">monkey testing</see>.
/// </summary>
public class MonkeyHttpClient(HttpClient httpClient, params MonkeyHttpClient.Monkey[] monkeys)
	: HttpClient
{
	/// <summary>Monkey callback that tampers with sending of HTTP requests.</summary>
	/// <remarks>Callback can delay HTTP request processing, it can throw an exception, it can employ randomization, etc.</remarks>
	public delegate Task Monkey();

	private HttpClient HttpClient { get; } = httpClient;

	public override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token = default)
	{
		foreach (Monkey monkey in monkeys)
		{
			await monkey().ConfigureAwait(false);
		}

		return await HttpClient.SendAsync(request, token).ConfigureAwait(false);
	}
}
