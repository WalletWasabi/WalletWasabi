using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tor.Http;

namespace WalletWasabi.Tests.Helpers;

public class HttpClientWrapper : IHttpClient
{
	public HttpClientWrapper(HttpClient httpClient)
	{
		HttpClient = httpClient;
	}

	private HttpClient HttpClient { get; }

	public Func<Uri>? BaseUriGetter => () => HttpClient.BaseAddress;

	public virtual Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token = default)
	{
		// HttpCompletionOption is required here because of a bug in dotnet.
		// without it the test fails randomly with ObjectDisposedException
		// see: https://github.com/dotnet/runtime/issues/23870
		return HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
	}
}

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
