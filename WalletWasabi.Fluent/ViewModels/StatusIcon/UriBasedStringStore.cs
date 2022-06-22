using System.Net.Http;
using System.Threading.Tasks;
using WalletWasabi.Tor;
using WalletWasabi.Tor.NetworkChecker;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Fluent.ViewModels.StatusIcon;

public class UriBasedStringStore : IUriBasedStringStore
{
	private readonly IWasabiHttpClientFactory _factory;

	public UriBasedStringStore()
	{
		_factory = GetFactory();
	}

	public async Task<string> Fetch(Uri uri)
	{
		using var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, uri);
		var httpClient = _factory.NewHttpClientWithDefaultCircuit();
		var response = await httpClient.SendAsync(httpRequestMessage);
		return await response.Content.ReadAsStringAsync();
	}

	private static IWasabiHttpClientFactory GetFactory()
	{
		if (Services.Config.UseTor)
		{
			return new HttpClientFactory(
				Services.TorSettings.SocksEndpoint,
				() => TorMonitor.RequestFallbackAddressUsage
					? Services.Config.GetFallbackBackendUri()
					: Services.Config.GetCurrentBackendUri());
		}

		return new HttpClientFactory(null, () => Services.Config.GetFallbackBackendUri());
	}
}
