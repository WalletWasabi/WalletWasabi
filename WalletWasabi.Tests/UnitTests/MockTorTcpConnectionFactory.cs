using System.Net.Http;

namespace WalletWasabi.Tests.UnitTests;

public class MockHttpClientFactory : IHttpClientFactory
{
	public Func<string, HttpClient>? OnCreateClient { get; set; }
	public HttpClient CreateClient(string name) =>
		OnCreateClient?.Invoke(name)
			?? throw new NotImplementedException($"{nameof(CreateClient)} was invoked but never assigned.");
}
