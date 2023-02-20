using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;

namespace WalletWasabi.WabiSabi.Backend.WebClients;

public class BlockstreamApiClient : BaseApiClient
{
	private static HttpClient SetBaseAddress(Network network, HttpClient httpClient)
	{
		httpClient.BaseAddress = new Uri(
			network == Network.TestNet
				? "https://blockstream.info/testnet/"
				: "https://blockstream.info/");
		
		return httpClient;
	}

	public BlockstreamApiClient(Network network, HttpClient httpClient) : base(SetBaseAddress(network, httpClient))
	{
		Network = network;
	}

	private Network Network { get; }
	public async Task<bool?> GetTransactionStatusAsync(uint256 txid, CancellationToken cancel)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, $"api/tx/{txid}");
		var response = await SendRequestAsync(request, "blockstream-request", cancel).ConfigureAwait(false);
		if (!response.IsSuccessStatusCode)
		{
			// Tx was not received or not accepted into mempool by blockstream's node.
			if (response.StatusCode == HttpStatusCode.NotFound)
			{
				return null;
			}
			throw new InvalidOperationException($"There was an unexpected error with request to Blockstream.{nameof(HttpStatusCode)} was {response?.StatusCode}.");
		}

		var document = await ParseResponseAsync(response, cancel).ConfigureAwait(false);
		var rootElement = document.RootElement;
		
		// Status has a block height field when the transaction is confirmed which is not extracted here.
		return rootElement.EnumerateObject().ToDictionary(x => x.Name)["status"].Value
			.EnumerateObject().First().Value.GetBoolean();
	}
}
