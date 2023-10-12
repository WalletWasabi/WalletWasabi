using NBitcoin;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Wallets;

public class TransactionFeeProvider
{
	public TransactionFeeProvider(WasabiHttpClientFactory httpClientFactory)
	{
		HttpClient = httpClientFactory.SharedWasabiClient;
	}

	public WasabiClient HttpClient { get; }

	public async Task<int?> FetchTransactionFeeAsync(uint256 txid)
	{
		using CancellationTokenSource cts = new(TimeSpan.FromSeconds(20));

		try
		{
			var response = await HttpClient.FetchTransactionFeeAsync(txid, cts.Token).ConfigureAwait(false);
			return response;
		}
		catch (Exception ex)
		{
			Logger.LogWarning($"Failed to fetch transaction fee. {ex}");
		}

		return null;
	}
}
