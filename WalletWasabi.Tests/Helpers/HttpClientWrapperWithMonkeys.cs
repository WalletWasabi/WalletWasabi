using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tor.Http;

namespace WalletWasabi.Tests.Helpers;

public class HttpClientWrapperWithMonkeys : IHttpClient
{
	public delegate Task Monkey();

	private Monkey[] _monkeys;

	public HttpClientWrapperWithMonkeys(IHttpClient httpClient, params Monkey[] monkeys)
	{
		HttpClient = httpClient;
		_monkeys = monkeys;
	}

	private IHttpClient HttpClient { get; }

	public Func<Uri>? BaseUriGetter => HttpClient.BaseUriGetter;

	public virtual async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token = default)
	{
		foreach (var monkey in _monkeys)
		{
			await monkey();
		}
		return await HttpClient.SendAsync(request, token);
	}
}