using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace WalletWasabi.Tests.UnitTests.Mocks;

public class MockHttpClientFactory : IHttpClientFactory
{
	public Func<string, HttpClient>? OnCreateClient { get; set; }
	public HttpClient CreateClient(string name) =>
		OnCreateClient?.Invoke(name)
			?? throw new NotImplementedException($"{nameof(CreateClient)} was invoked but never assigned.");


	public static MockHttpClientFactory Create(params Func<HttpResponseMessage>[] responses)
	{
#pragma warning disable CA2000 // Dispose objects before losing scope - MockHttpClient is returned via factory and disposed by caller
		var mockHttpClient = new MockHttpClient();
#pragma warning restore CA2000
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

public static class HttpResponseMessageEx
{
	public static HttpResponseMessage Ok(string str)
	{
		HttpResponseMessage response = new(HttpStatusCode.OK);
		response.Content = new StringContent(str);
		return response;
	}
}
