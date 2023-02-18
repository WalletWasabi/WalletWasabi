using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Http.Extensions;

namespace WalletWasabi.WabiSabi.Backend.WebClients;

public class BlockstreamApiClient
{
	public BlockstreamApiClient(Network network, HttpClient httpClient)
	{
		HttpClient = httpClient;
		BaseAddress = network == Network.TestNet
			? "https://blockstream.info/testnet/"
			: "https://blockstream.info/";
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

	// Don't throw as this task is not awaited
	public async Task LogTxAcceptedByBlockstreamAsync(uint256 txid, CancellationToken cancel, int delay = 30, int attempts = 10)
	{
		for (int i = 0; i < attempts; i++)
		{
			bool? result = null;
			try
			{
				await Task.Delay(delay, cancel).ConfigureAwait(false);
				result = await GetTransactionStatusAsync(txid, cancel).ConfigureAwait(false);
			}
			catch (Exception e)
			{
				if (cancel.IsCancellationRequested)
				{
					return;
				}

				if (i + 1 == attempts)
				{
					Logger.LogWarning($"Coinjoin TX {txid} couldn't be tested against Blockstream's mempool. Probably API service is unavailable.");
					return;
				}
			}

			if (result is not null)
			{
				// TX is in mempool or confirmed.
				Logger.LogTrace($"Coinjoin TX {txid} was accepted by Blockstream's node in less than {delay * (i+1)} seconds.");
				return;
			}
		}
		
		Logger.LogWarning($"Coinjoin TX {txid} hasn't been accepted by Blockstream's node after {delay * attempts} seconds.");
	}
}
