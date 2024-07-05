using Microsoft.Extensions.Hosting;
using NBitcoin;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Http.Extensions;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Wallets;

public class UnconfirmedTransactionChainProvider : BackgroundService
{
	private const int MaximumDelayInSeconds = 120;
	private const int MaximumRequestsInParallel = 3;
	private static readonly TimeSpan MinimumBetweenUpdateRequests = TimeSpan.FromMinutes(2);

	public UnconfirmedTransactionChainProvider(WasabiHttpClientFactory httpClientFactory)
	{
		HttpClient = httpClientFactory.NewHttpClient(() => new Uri("https://mempool.space/api/"), Tor.Socks5.Pool.Circuits.Mode.NewCircuitPerRequest);
	}

	public event EventHandler<EventArgs>? RequestedUnconfirmedChainArrived;

	private ConcurrentDictionary<uint256, CachedUnconfirmedTransactionChain> UnconfirmedChainCache { get; } = new();

	private IHttpClient HttpClient { get; }

	private Channel<SmartTransaction> Channel { get; } = System.Threading.Channels.Channel.CreateUnbounded<SmartTransaction>();

	private DateTime LastUpdateRequest { get; set; } = DateTime.MinValue;
	private List<uint256> UpdateRequested { get; } = [];

	private async Task FetchUnconfirmedTransactionChainAsync(SmartTransaction transaction, CancellationToken cancellationToken)
	{
		const int MaxAttempts = 3;

		var txid = transaction.GetHash();

		for (int i = 0; i < MaxAttempts; i++)
		{
			try
			{
				using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(60));
				using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

				var response = await HttpClient.SendAsync(
					HttpMethod.Get,
					$"v1/cpfp/{txid}",
					null,
					linkedCts.Token).ConfigureAwait(false);

				if (response.StatusCode != HttpStatusCode.OK)
				{
					await response.ThrowRequestExceptionFromContentAsync(cancellationToken).ConfigureAwait(false);
				}

				var unconfirmedChain = await response.Content.ReadAsJsonAsync<UnconfirmedTransactionChain>().ConfigureAwait(false);

				UnconfirmedChainCache.AddOrReplace(txid, new CachedUnconfirmedTransactionChain(unconfirmedChain, transaction));

				RequestedUnconfirmedChainArrived?.Invoke(this, EventArgs.Empty);

				return;
			}
			catch (OperationCanceledException)
			{
				Logger.LogTrace("Request was cancelled by exiting the app.");
			}
			catch (Exception ex)
			{
				Logger.LogWarning($"Attempt: {i}. Failed to fetch transaction fee. {ex}");
			}
		}
	}

	private bool ShouldRequest(SmartTransaction tx, bool useCache = true)
	{
		if (tx.Confirmed ||
		    (tx.ForeignInputs.Count == 0 && tx.GetInputs().All(x => x.Confirmed.GetValueOrDefault())))
		{
			return false;
		}

		if (!useCache)
		{
			return true;
		}

		return !UnconfirmedChainCache.ContainsKey(tx.GetHash());
	}

	public void ScheduleRequest(SmartTransaction tx)
	{
		if (!ShouldRequest(tx))
		{
			return;
		}
		Channel.Writer.TryWrite(tx);
	}

	public async Task<UnconfirmedTransactionChain?> ImmediateRequestAsync(SmartTransaction tx, CancellationToken cancellationToken)
	{
		if(!ShouldRequest(tx, false))
		{
			return null;
		}

		await FetchUnconfirmedTransactionChainAsync(tx, cancellationToken).ConfigureAwait(false);
		return UnconfirmedChainCache[tx.GetHash()].Chain;
	}

	protected override async Task ExecuteAsync(CancellationToken cancel)
	{
		List<Task> tasks = [];
		while (!cancel.IsCancellationRequested)
		{
			var txidToFetch = await Channel.Reader.ReadAsync(cancel).ConfigureAwait(false);

			tasks.Add(Task.Run(() => ScheduledTask(txidToFetch), cancel));

			if (tasks.Count >= MaximumRequestsInParallel)
			{
				Task completedTask = await Task.WhenAny(tasks).ConfigureAwait(false);
				tasks.Remove(completedTask);
			}
		}

		async Task ScheduledTask(SmartTransaction transaction)
		{
			var random = new Random();
			var delayInSeconds = random.Next(MaximumDelayInSeconds);
			var delay = TimeSpan.FromSeconds(delayInSeconds);

			try
			{
				await Task.Delay(delay, cancel).ConfigureAwait(false);

				await FetchUnconfirmedTransactionChainAsync(transaction, cancel).ConfigureAwait(false);

				var txid = transaction.GetHash();
				UpdateRequested.Remove(txid);
			}
			catch (OperationCanceledException)
			{
				Logger.LogTrace("Request was cancelled by exiting the app.");
			}
			catch (Exception e)
			{
				Logger.LogWarning(e);
			}
		}
	}

	public void UpdateCache()
	{
		if (LastUpdateRequest + MinimumBetweenUpdateRequests > DateTime.UtcNow)
		{
			return;
		}

		LastUpdateRequest = DateTime.UtcNow;

		var snapshot = UnconfirmedChainCache.ToList();
		foreach (var cachedChain in snapshot.Where(cachedChain => !UpdateRequested.Contains(cachedChain.Key)))
		{
			UpdateRequested.Add(cachedChain.Key);
			ScheduleRequest(cachedChain.Value.Transaction);
		}
	}

	public UnconfirmedTransactionChain? GetUnconfirmedTransactionChain(uint256 txId)
	{
		return UnconfirmedChainCache.TryGet(txId).Chain;
	}

	private record CachedUnconfirmedTransactionChain(UnconfirmedTransactionChain Chain, SmartTransaction Transaction);
}
