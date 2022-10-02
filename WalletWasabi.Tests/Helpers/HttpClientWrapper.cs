using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tor.Http;
using WalletWasabi.WabiSabi.Backend.Statistics;

namespace WalletWasabi.Tests.Helpers;

public class HttpClientWrapper : IHttpClient
{
	public HttpClientWrapper(HttpClient httpClient)
	{
		HttpClient = httpClient;
	}

	private HttpClient HttpClient { get; }

	public Func<Uri>? BaseUriGetter => () => HttpClient.BaseAddress;

	public virtual Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, StatsLogger? statsLogger = null, CancellationToken token = default)
	{
		// HttpCompletionOption is required here because of a bug in dotnet.
		// without it the test fails randomly with ObjectDisposedException
		// see: https://github.com/dotnet/runtime/issues/23870
		return HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
	}
}
