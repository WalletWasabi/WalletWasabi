using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using Nito.AsyncEx;
using WalletWasabi.Tor.Http.Extensions;
using WalletWasabi.WabiSabi.Backend.Statistics;
using WalletWasabi.WabiSabi.Backend.WebClients.Models;

namespace WalletWasabi.WabiSabi.Backend.WebClients;

public class MempoolSpaceApiClient : ITxPropagationVerifier
{
	public MempoolSpaceApiClient(Network network, HttpClient httpClient)
	{
		Network = network;
		httpClient.BaseAddress = new Uri(
			network == Network.TestNet
				? "https://mempool.space/testnet/"
				: "https://mempool.space/");
		HttpClient = httpClient;
	}

	private Network Network { get; }
	private HttpClient HttpClient { get; }
	private AsyncLock AsyncLock { get; } = new();
	public async Task<bool?> GetTransactionStatusAsync(uint256 txid, CancellationToken cancel)
	{
		HttpResponseMessage response;
		using (await AsyncLock.LockAsync(cancel).ConfigureAwait(false))
		{
			// Ensure not being banned by Mempool.space's API
			await Task.Delay(1000, cancel).ConfigureAwait(false);
			
			using var request = new HttpRequestMessage(HttpMethod.Get, $"api/tx/{txid}");
			var stopWatch = Stopwatch.StartNew();
			response = await HttpClient.SendAsync(request, cancel).ConfigureAwait(false);
			stopWatch.Stop();
			RequestTimeStatista.Instance.Add("mempoolspace-request", stopWatch.Elapsed);
		}

		if (!response.IsSuccessStatusCode)
		{
			// Tx was not received or not accepted into mempool by blockstream's node.
			if (response.StatusCode == HttpStatusCode.NotFound)
			{
				return null;
			}
			throw new InvalidOperationException($"There was an unexpected error with request to mempool.space.{nameof(HttpStatusCode)} was {response?.StatusCode}.");
		}

		var document = await response.Content.ReadAsJsonAsync<MemPoolSpaceApiResponseItem>().ConfigureAwait(false);

		// Status has a block height field when the transaction is confirmed which is not extracted here.
		return document.status.confirmed;
	}
}
