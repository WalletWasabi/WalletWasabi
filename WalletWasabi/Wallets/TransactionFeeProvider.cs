using Microsoft.Extensions.Hosting;
using NBitcoin;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Http.Extensions;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Wallets;

public class TransactionFeeProvider : BackgroundService
{
	private const int MaximumDelayInSeconds = 120;
	private const int MaximumRequestsInParallel = 3;

	public TransactionFeeProvider(WasabiHttpClientFactory httpClientFactory)
	{
		HttpClient = httpClientFactory.NewHttpClient(httpClientFactory.BackendUriGetter, Tor.Socks5.Pool.Circuits.Mode.NewCircuitPerRequest);
	}

	public event EventHandler<EventArgs>? RequestedFeeArrived;

	public ConcurrentDictionary<uint256, Money> FeeCache { get; } = new();
	public Channel<uint256> TransactionIdChannel { get; } = Channel.CreateUnbounded<uint256>();
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
			TransactionIdChannel.Writer.TryWrite(tx.GetHash());
		}
	}

	protected override async Task ExecuteAsync(CancellationToken cancel)
	{
		List<Task> activeTasks = new(capacity: MaximumRequestsInParallel);
		while (!cancel.IsCancellationRequested)
		{
			if (activeTasks.Count >= MaximumRequestsInParallel)
			{
				continue;
			}

			if (TransactionIdChannel.Reader.TryRead(out var txidToFetch) && txidToFetch is not null)
			{
				var task = Task.Run(async () => await ScheduledTask(txidToFetch).ConfigureAwait(false), cancel);
				activeTasks.Add(task);
			}

			if (activeTasks.Count == 0)
			{
				continue;
			}

			var completedTask = await Task.WhenAny(activeTasks).ConfigureAwait(false);
			activeTasks.Remove(completedTask);
		}

		async Task ScheduledTask(uint256 txid)
		{
			var random = new Random();
			var delayInSeconds = random.Next(MaximumDelayInSeconds);
			var delay = TimeSpan.FromSeconds(delayInSeconds);

			await Task.Delay(delay, cancel).ConfigureAwait(false);

			await FetchTransactionFeeAsync(txid, cancel).ConfigureAwait(false);
		}
	}
}
