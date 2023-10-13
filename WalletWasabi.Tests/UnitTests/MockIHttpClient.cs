using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tor.Http;

namespace WalletWasabi.Tests.UnitTests;

public class MockIHttpClient : IHttpClient
{
	public MockIHttpClient()
		: this("https://fake.domain.com")
	{ }

	public MockIHttpClient(string uri)
	{
		BaseUriGetter = () => new Uri(uri);
	}

	public Func<Uri>? BaseUriGetter { get; }
	public Func<HttpRequestMessage, Task<HttpResponseMessage>>? OnSendAsync { get; set; }

	public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default) =>
		OnSendAsync?.Invoke(request) ?? throw new NotImplementedException();
}

public class MockHttpClient : HttpClient
{
	public Func<HttpRequestMessage, Task<HttpResponseMessage>>? OnSendAsync { get; set; }

	public override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
		OnSendAsync?.Invoke(request) ?? throw new NotImplementedException();
}

public static class MockHttpClientExtensions
{
	public static void SetupSequence(this MockHttpClient http, params Func<HttpResponseMessage>[] responses)
	{
		var callCounter = 0;
		http.OnSendAsync = req =>
		{
			var responseFn = responses[callCounter];
			Interlocked.Increment(ref callCounter);
			return Task.FromResult(responseFn());
		};
	}
}
