using NBitcoin;
using WalletWasabi.Tor.Http;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tor.Http.Extensions;
using System.Net.Http;
using WalletWasabi.WebClients.Wasabi;
using WalletWasabi.Tor.Socks5.Pool.Circuits;

namespace WalletWasabi.WebClients.MempoolSpace;
public class MempoolSpaceApiClient
{
	public MempoolSpaceApiClient(WasabiHttpClientFactory httpClientFactory, Network network)
	{
		string uriString;

		if (httpClientFactory.IsTorEnabled)
		{
			uriString = network == Network.TestNet
				? "http://mempoolhqx4isw62xs7abwphsq7ldayuidyx2v2oethdhhj6mlo2r6ad.onion/testnet/"
				: "http://mempoolhqx4isw62xs7abwphsq7ldayuidyx2v2oethdhhj6mlo2r6ad.onion/";
		}
		else
		{
			uriString = network == Network.TestNet
				? "https://mempool.space/testnet/"
				: "https://mempool.space/";
		}

		HttpClient = httpClientFactory.NewHttpClient(() => new Uri(uriString), Mode.NewCircuitPerRequest);
	}

	private IHttpClient HttpClient { get; }
	public async Task<MempoolSpaceApiResponseItem> GetTransactionInfosAsync(uint256 txid, CancellationToken cancel)
	{
		HttpResponseMessage response;

		// Ensure not being banned by Mempool.space's API
		await Task.Delay(1000, cancel).ConfigureAwait(false);

		response = await HttpClient.SendAsync(HttpMethod.Get, $"api/tx/{txid}", null, cancel).ConfigureAwait(false);

		if (!response.IsSuccessStatusCode)
		{
			// Tx was not found in mempool.space's node.
			await response.ThrowRequestExceptionFromContentAsync(cancel).ConfigureAwait(false);
		}

		return await response.Content.ReadAsJsonAsync<MempoolSpaceApiResponseItem>().ConfigureAwait(false);
	}
}
