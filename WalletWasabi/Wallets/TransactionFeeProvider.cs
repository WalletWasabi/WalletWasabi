using NBitcoin;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Logging;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Wallets;

public class TransactionFeeProvider : PeriodicRunner
{
	public TransactionFeeProvider(WasabiHttpClientFactory httpClientFactory) : base(TimeSpan.FromSeconds(10))
	{
		HttpClient = httpClientFactory.SharedWasabiClient;
	}

#pragma warning disable IDE1006 // Naming Styles
	public ConcurrentDictionary<uint256, int> FeeCache = new();
#pragma warning restore IDE1006 // Naming Styles
	public ConcurrentQueue<uint256> Queue { get; } = new();

	private WasabiClient HttpClient { get; }

	private async Task FetchTransactionFeeAsync(uint256 txid, CancellationToken cancellationToken)
	{
		using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(20));
		using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

		try
		{
			int feeInSats = await HttpClient.FetchTransactionFeeAsync(txid, linkedCts.Token).ConfigureAwait(false);

			if (!FeeCache.TryAdd(txid, feeInSats))
			{
				throw new InvalidOperationException($"Failed to cache {txid} with fee: {feeInSats}");
			}
		}
		catch (Exception ex)
		{
			Logger.LogWarning($"Failed to fetch transaction fee. {ex}");
		}
	}

	protected override Task ActionAsync(CancellationToken cancel)
	{
		while (!Queue.IsEmpty)
		{
			if (Queue.TryDequeue(out var txid))
			{
				Task.Run(async () => await FetchTransactionFeeAsync(txid, cancel).ConfigureAwait(false), cancel);
			}
		}

		return Task.CompletedTask;
	}

	public int GetFee(uint256 txid)
	{
		if (FeeCache.TryGetValue(txid, out var fee))
		{
			return fee;
		}

		return 0;
	}

	public void WalletRelevantTransactionProcessed(object? sender, ProcessedResult e)
	{
		if (!e.Transaction.Confirmed && e.Transaction.ForeignInputs.Any())
		{
			Queue.Enqueue(e.Transaction.GetHash());
		}
	}
}
