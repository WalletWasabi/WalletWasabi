using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Tor.Http.Extensions;

namespace WalletWasabi.WabiSabi.Backend.WebClients;

public class MempoolSpaceApiClient
{
	public MempoolSpaceApiClient(Network network, HttpClient httpClient)
	{
		HttpClient = httpClient;
		BaseAddress = network == Network.TestNet
			? "https://mempool.space/testnet/"
			: "https://mempool.space/";
	}
	
	private string BaseAddress { get; set; }

	private HttpClient HttpClient { get; set; }

	public async Task<bool?> GetTransactionStatusAsync(uint256 txid, CancellationToken cancel)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseAddress}api/tx/{txid}");
		var response = await HttpClient.SendAsync(request, cancel).ConfigureAwait(false);
		if (!response.IsSuccessStatusCode)
		{
			// Tx was not received or not accepted into mempool by blockstream's node.
			if (response.StatusCode == HttpStatusCode.NotFound)
			{
				return null;
			}
			
			// Error with the request.
			await response.ThrowRequestExceptionFromContentAsync(cancel).ConfigureAwait(false);
		}

		var responseString = await response.Content.ReadAsStringAsync(cancel).ConfigureAwait(false);
		var parsed = JsonDocument.Parse(responseString).RootElement;
		
		// Status has a block height field when the transaction is confirmed which is not extracted here.
		return parsed.EnumerateObject().ToDictionary(x => x.Name)["status"].Value
			.EnumerateObject().First().Value.GetBoolean();
	}
}
