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
	}
	private HttpClient HttpClient { get; }
	private Network Network { get; }
	public BlockstreamApiClient BlockstreamApiClient { get; }
	
	// Don't throw as this task is not awaited
	public async Task LogTxAcceptedByBlockstreamAsync(uint256 txid, CancellationToken cancel, int delay = 30, int attempts = 10)
	{
		for (int i = 0; i < attempts; i++)
		{
			bool? result = null;
			try
			{
				await Task.Delay(delay, cancel).ConfigureAwait(false);
				result = await BlockstreamApiClient.GetTransactionStatusAsync(txid, cancel).ConfigureAwait(false);
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
