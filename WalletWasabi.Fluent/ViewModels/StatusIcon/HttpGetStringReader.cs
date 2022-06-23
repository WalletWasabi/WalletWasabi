using System.Net.Http;
using System.Threading.Tasks;
using WalletWasabi.Tor.NetworkChecker;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Fluent.ViewModels.StatusIcon;

public class HttpGetStringReader : IHttpGetTextReader
{
	private readonly IWasabiHttpClientFactory _factory;

	public HttpGetStringReader(IWasabiHttpClientFactory wasabiHttpClientFactory)
	{
		_factory = wasabiHttpClientFactory;
	}

	public async Task<string> Read(Uri uri)
	{
		using var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, uri);
		var httpClient = _factory.NewHttpClientWithDefaultCircuit();
		var response = await httpClient.SendAsync(httpRequestMessage);
		var content = await response.Content.ReadAsStringAsync();
		return content;
	}
}
