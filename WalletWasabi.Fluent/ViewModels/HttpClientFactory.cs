using System.Net.Http;

namespace WalletWasabi.Fluent.ViewModels;

public class HttpClientFactory : IHttpClientFactory
{
	public HttpClient CreateClient(string name)
	{
		return new HttpClient();
	}
}