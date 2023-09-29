using NBitcoin;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi.WebClients.MempoolSpace;

public class TransactionFeeFetcher
{
	public TransactionFeeFetcher(MempoolSpaceApiClient mempoolSpaceApiClient)
	{
		MempoolSpaceApiClient = mempoolSpaceApiClient;
	}

	private MempoolSpaceApiClient MempoolSpaceApiClient { get; set; }

	public async Task<int?> FetchTransactionFeeAsync(uint256 txid)
	{
		using CancellationTokenSource cts = new(TimeSpan.FromSeconds(20));

		try
		{
			var response = await MempoolSpaceApiClient.GetTransactionInfosAsync(txid, cts.Token).ConfigureAwait(false);
			return response.Fee;
		}
		catch (Exception ex)
		{
			Logger.LogWarning($"Failed to fetch transaction fee. {ex}");
		}
		
		return null;
	}
}
