using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Backend.WebClients;

namespace WalletWasabi.WabiSabi.Backend.Statistics;

public class TxPropagationVerifier
{
	public TxPropagationVerifier(Network network, HttpClient httpClient)
	{
		HttpClient = httpClient;
		Network = network;
		BlockstreamApiClient = new BlockstreamApiClient(Network, HttpClient);
		MempoolSpaceApiClient = new MempoolSpaceApiClient(Network, HttpClient);
	}
	private HttpClient HttpClient { get; }
	private Network Network { get; }
	public BlockstreamApiClient BlockstreamApiClient { get; }
	public MempoolSpaceApiClient MempoolSpaceApiClient { get; }
	
	// Don't throw as this task is not awaited
	public async Task LogTxAcceptedByBlockstreamAsync(uint256 txid, CancellationToken cancel, int delay = 30, int attempts = 10)
	{
		for (int i = 0; i < attempts; i++)
		{
			try
			{
				await Task.Delay(delay, cancel).ConfigureAwait(false);
				List<Task<bool?>> tasks = new List<Task<bool?>>()
				{
					BlockstreamApiClient.GetTransactionStatusAsync(txid, cancel),
					MempoolSpaceApiClient.GetTransactionStatusAsync(txid, cancel),
				};
				var results = await Task.WhenAll(tasks).ConfigureAwait(false);
				
				if (results.Any(result => result is not null))
				{
					Logger.LogTrace($"Coinjoin TX {txid} was accepted by third party node in less than {delay * (i+1)} seconds.");
					return;
				}
			}
			catch (Exception e)
			{
				if (cancel.IsCancellationRequested)
				{
					return;
				}

				if (i + 1 == attempts)
				{
					Logger.LogWarning($"Coinjoin TX {txid} couldn't be tested against thrid party mempool. Probably API service is unavailable.");
					return;
				}
			}
		}
		
		Logger.LogWarning($"Coinjoin TX {txid} hasn't been accepted by third party node after {delay * attempts} seconds.");
	}
}
