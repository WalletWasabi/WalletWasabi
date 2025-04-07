using System.Net;
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


	public static MockHttpClientFactory Create(params Func<HttpResponseMessage>[] responses)
	{
		var mockHttpClient = new MockHttpClient();
		var mockHttpClientFactory = new MockHttpClientFactory {OnCreateClient = _ => mockHttpClient};

		var callCounter = 0;
		mockHttpClient.OnSendAsync = _ =>
		{
			var responseFn = responses[callCounter];
			callCounter++;
			return Task.FromResult(responseFn());
		};
		return mockHttpClientFactory;
	}
}

public class MockHttpClientHandler : HttpClientHandler
{
	public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> OnSendAsync;

	protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
		OnSendAsync(request, cancellationToken);
}

public static class HttpResponseMessageEx
{
	public static HttpResponseMessage Ok(string str)
	{
		HttpResponseMessage response = new(HttpStatusCode.OK);
		response.Content = new StringContent(str);
		return response;
	}
}
