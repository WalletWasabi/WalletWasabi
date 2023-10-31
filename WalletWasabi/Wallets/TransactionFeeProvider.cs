using NBitcoin;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Http.Extensions;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Wallets;

public class TransactionFeeProvider : PeriodicRunner
{
	public TransactionFeeProvider(WasabiHttpClientFactory httpClientFactory) : base(TimeSpan.FromSeconds(10))
	{
		HttpClient = httpClientFactory.NewHttpClient(httpClientFactory.BackendUriGetter, Tor.Socks5.Pool.Circuits.Mode.NewCircuitPerRequest);
	}

	public ConcurrentDictionary<uint256, Money> FeeCache { get; } = new();
	public ConcurrentQueue<uint256> Queue { get; } = new();

	private IHttpClient HttpClient { get; }

	private async Task FetchTransactionFeeAsync(uint256 txid, CancellationToken cancellationToken)
	{
		using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(20));
		using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

		try
		{
			var response = await HttpClient.SendAsync(HttpMethod.Get, $"api/v{Helpers.Constants.BackendMajorVersion}/btc/Blockchain/get-transaction-fee?transactionId={txid}", null, cancellationToken).ConfigureAwait(false);

			if (response.StatusCode != HttpStatusCode.OK)
			{
				await response.ThrowRequestExceptionFromContentAsync(cancellationToken).ConfigureAwait(false);
			}

			Money fee = await response.Content.ReadAsJsonAsync<Money>().ConfigureAwait(false);

			if (!FeeCache.TryAdd(txid, fee))
			{
				throw new InvalidOperationException($"Failed to cache {txid} with fee: {fee}");
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

	public Money GetFee(uint256 txid)
	{
		if (FeeCache.TryGetValue(txid, out var fee))
		{
			return fee;
		}

		return Money.Zero;
	}

	public void WalletRelevantTransactionProcessed(object? sender, ProcessedResult e)
	{
		if (!e.Transaction.Confirmed && e.Transaction.ForeignInputs.Any())
		{
			Queue.Enqueue(e.Transaction.GetHash());
		}
	}
}
