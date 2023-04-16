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
		httpClient.BaseAddress = new Uri(
			network == Network.TestNet
				? "https://mempool.space/testnet/"
				: "https://mempool.space/");
		HttpClient = httpClient;
	}
	
	private HttpClient HttpClient { get; }
	private AsyncLock AsyncLock { get; } = new();

	public async Task<bool> IsTxAcceptedByNode(uint256 txid, CancellationToken cancel)
	{
		MempoolSpaceApiResponseItem? apiResponse = await GetTransactionInfosAsync(txid, cancel).ConfigureAwait(false);
		return apiResponse is { };
	}

	private async Task<MempoolSpaceApiResponseItem?> GetTransactionInfosAsync(uint256 txid, CancellationToken cancel)
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
			// Tx was not received or not accepted into mempool by mempool.space's node.
			if (response.StatusCode == HttpStatusCode.NotFound)
			{
				return null;
			}
			throw new InvalidOperationException($"There was an unexpected error with request to mempool.space.{nameof(HttpStatusCode)} was {response?.StatusCode}.");
		}

		return await response.Content.ReadAsJsonAsync<MempoolSpaceApiResponseItem>().ConfigureAwait(false);
	}
}
