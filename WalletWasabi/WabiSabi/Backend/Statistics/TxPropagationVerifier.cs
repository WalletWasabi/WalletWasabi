using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Backend.WebClients;

namespace WalletWasabi.WabiSabi.Backend.Statistics;

public class TxPropagationVerifier
{
	public TxPropagationVerifier(Network network, IHttpClientFactory httpClientFactory)
	{
		Verifiers = new List<ITxPropagationVerifierApiClient>()
		{
			new BlockstreamApiClient(network, httpClientFactory.CreateClient(nameof(BlockstreamApiClient))),
			new MempoolSpaceApiClient(network, httpClientFactory.CreateClient(nameof(MempoolSpaceApiClient)))
		};
	}
	public List<ITxPropagationVerifierApiClient> Verifiers { get; }

	// Don't throw as this task is not awaited
	public async Task LogTxAcceptedByThirdPartyAsync(uint256 txid, CancellationToken cancel)
	{
		const int RetryEachSeconds = 30;
		const int Attempts = 10;

		var delay = TimeSpan.FromSeconds(RetryEachSeconds);

		var lastNegativeResult = 0;
		for (var i = 0; i < Attempts; i++)
		{
			if (cancel.IsCancellationRequested)
			{
				return;
			}
			
			await Task.Delay(delay, cancel).ConfigureAwait(false);
			var tasks = new List<Task<bool>>();
			foreach (var txPropagationVerifier in Verifiers)
			{
				tasks.Add(txPropagationVerifier.IsTxAcceptedByNode(txid, cancel));
			}

			while (tasks.Count > 0)
			{
				var completedTask = await Task.WhenAny(tasks).ConfigureAwait(false);
				tasks = tasks.Except(new[] { completedTask }).ToList();

				bool result;
				try
				{
					result = await completedTask.ConfigureAwait(false);
				}
				catch
				{
					if (cancel.IsCancellationRequested)
					{
						return;
					}

					// Call to this API provider failed, check result for next one.
					continue;
				}

				if (result)
				{
					Logger.LogInfo($"Coinjoin TX {txid} was accepted by third party node in less than {delay.Seconds * (i + 1)} seconds.");
					return;
				}

				// Call to this API provider was successful, but the transaction was not present in its mempool
				lastNegativeResult = i + 1;
			}
		}

		Logger.LogWarning(
			lastNegativeResult != 0 ? 
			$"Coinjoin TX {txid} hasn't been accepted by third party node after {delay.Seconds * (lastNegativeResult)} seconds." : 
			$"Coinjoin TX {txid} couldn't be tested against any third party node in {delay.Seconds * Attempts} seconds.");
	}
}
