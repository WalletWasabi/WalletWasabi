using Microsoft.Extensions.Hosting;
using NBitcoin;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Extensions;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Http.Extensions;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Wallets;

public class TransactionFeeProvider : BackgroundService
{
	private readonly int _maximumDelayInSeconds = 120;

	public TransactionFeeProvider(WasabiHttpClientFactory httpClientFactory)
	{
		HttpClient = httpClientFactory.NewHttpClient(httpClientFactory.BackendUriGetter, Tor.Socks5.Pool.Circuits.Mode.NewCircuitPerRequest);
	}

	public event EventHandler<EventArgs>? RequestedFeeArrived;

	public ConcurrentDictionary<uint256, Money> FeeCache { get; } = new();
	public ConcurrentQueue<uint256> Queue { get; } = new();
	private SemaphoreSlim Semaphore { get; } = new(initialCount: 0);

	private IHttpClient HttpClient { get; }

	private async Task FetchTransactionFeeAsync(uint256 txid, CancellationToken cancellationToken)
	{
		const int MaxAttempts = 3;

		for (int i = 0; i < MaxAttempts; i++)
		{
			try
			{
				using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(20));
				using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

				var response = await HttpClient.SendAsync(
					HttpMethod.Get,
					$"api/v{Helpers.Constants.BackendMajorVersion}/btc/Blockchain/get-transaction-fee?transactionId={txid}",
					null,
					linkedCts.Token).ConfigureAwait(false);

				if (response.StatusCode != HttpStatusCode.OK)
				{
					await response.ThrowRequestExceptionFromContentAsync(cancellationToken).ConfigureAwait(false);
				}

				Money fee = await response.Content.ReadAsJsonAsync<Money>().ConfigureAwait(false);

				if (!FeeCache.TryAdd(txid, fee))
				{
					throw new InvalidOperationException($"Failed to cache {txid} with fee: {fee}");
				}

				RequestedFeeArrived?.Invoke(this, EventArgs.Empty);
				return;
			}
			catch (Exception ex)
			{
				if (cancellationToken.IsCancellationRequested)
				{
					return;
				}

				Logger.LogWarning($"Attempt: {i}. Failed to fetch transaction fee. {ex}");
			}
		}
	}

	public bool TryGetFeeFromCache(uint256 txid, [NotNullWhen(true)] out Money? fee)
	{
		return FeeCache.TryGetValue(txid, out fee);
	}

	public void BeginRequestTransactionFee(SmartTransaction tx)
	{
		if (!tx.Confirmed && tx.ForeignInputs.Count != 0)
		{
			Queue.Enqueue(tx.GetHash());
			Semaphore.Release(1);
		}
	}

	protected override async Task ExecuteAsync(CancellationToken cancel)
	{
		while (!cancel.IsCancellationRequested)
		{
			await Semaphore.WaitAsync(cancel).ConfigureAwait(false);

			if (!Queue.TryDequeue(out var txidToFetch))
			{
				continue;
			}

			// We are not observing the result, because it cannot throw and we are retrying within the function
			_ = ScheduledTask(txidToFetch);
		}

		async Task ScheduledTask(uint256 txid)
		{
			var random = new Random();
			var delayInSeconds = random.Next(_maximumDelayInSeconds);
			var delay = TimeSpan.FromSeconds(delayInSeconds);

			await Task.Delay(delay, cancel).ConfigureAwait(false);

			await FetchTransactionFeeAsync(txid, cancel).ConfigureAwait(false);
		}
	}

	public override void Dispose()
	{
		Semaphore.Dispose();
		base.Dispose();
	}
}
