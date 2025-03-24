using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Tests.UnitTests;

public class MockHttpClientFactory : IHttpClientFactory
{
	public Func<string, HttpClient>? OnCreateClient { get; set; }
	public HttpClient CreateClient(string name) =>
		OnCreateClient?.Invoke(name)
			?? throw new NotImplementedException($"{nameof(CreateClient)} was invoked but never assigned.");
}

public class MockHttpClientHandler : HttpClientHandler
{
	public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> OnSendAsync;

	protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
		OnSendAsync(request, cancellationToken);
}

