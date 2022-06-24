using System.Net.Http;
using System.Threading.Tasks;
using WalletWasabi.Tor.Http;

namespace WalletWasabi.Fluent.ViewModels.StatusIcon;

public class HttpGetStringReader
{
	private readonly IHttpClient _wasabiHttpClient;

	public HttpGetStringReader(IHttpClient wasabiHttpClient)
	{
		_wasabiHttpClient = wasabiHttpClient;
	}

	public async Task<string> ReadAsync(Uri uri)
	{
		using var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, uri);
		var response = await _wasabiHttpClient.SendAsync(httpRequestMessage).ConfigureAwait(false);
		var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
		return content;
	}
}
