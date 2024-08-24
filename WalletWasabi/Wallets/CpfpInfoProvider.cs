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
using Newtonsoft.Json;
using Nito.AsyncEx;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Extensions;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Http.Extensions;
using WalletWasabi.WabiSabi.Models.Serialization;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Wallets;

public class CpfpInfoProvider : BackgroundService
{
	private readonly IHttpClientFactory _httpClientFactory;
	private const int MaximumDelayInSeconds = 120;
	private const int MaximumScheduledRequestsInParallel = 15;
	private static readonly TimeSpan MinimumBetweenUpdateRequests = TimeSpan.FromSeconds(MaximumDelayInSeconds);


	public CpfpInfoProvider(IHttpClientFactory httpClientFactory, Network network)
	{
		_httpClientFactory = httpClientFactory;

		_uri = network == Network.Main
			? new Uri("https://mempool.space/api/")
			: network == Network.TestNet
				? new Uri("https://mempool.space/testnet/api/")
				: throw new InvalidOperationException("CpfpInfoProvider is only operational on Main or TestNet");
	}

	private readonly Uri _uri;

	private readonly Channel<SmartTransaction> _transactionsChannel = Channel.CreateUnbounded<SmartTransaction>();
	private readonly Dictionary<uint256, CachedCpfpInfo> _cpfpInfoCache = new();
	private AsyncLock AsyncLock { get; } = new();

	private DateTimeOffset _lastUpdateCacheLoop = DateTimeOffset.MinValue;

	public event EventHandler<EventArgs>? RequestedCpfpInfoArrived;

	protected override async Task ExecuteAsync(CancellationToken cancel)
	{
		List<uint256> scheduledRequests = [];
		List<Task> tasks = [];
		while (!cancel.IsCancellationRequested)
		{

			tasks.RemoveAll(t => t.IsCompleted);

			while (tasks.Count >= MaximumScheduledRequestsInParallel)
			{
				var completedTask = await Task.WhenAny(tasks).ConfigureAwait(false);
				tasks.Remove(completedTask);
			}

			var txToFetch = await _transactionsChannel.Reader.ReadAsync(cancel).ConfigureAwait(false);
			var txid = txToFetch.GetHash();

			using (await AsyncLock.LockAsync(cancel).ConfigureAwait(false))
			{
				if (scheduledRequests.Contains(txid) ||
				    !ShouldRequest(txToFetch) ||
				    (_cpfpInfoCache.TryGetValue(txid, out var cachedCpfpInfo) &&
				     cachedCpfpInfo.TimeLastUpdate > DateTimeOffset.UtcNow - MinimumBetweenUpdateRequests))
				{
					// We already scheduled fetch or fetching doesn't make sense anymore or last result is recent enough.
					continue;
				}
			}

			scheduledRequests.Add(txid);
			tasks.Add(Task.Run(() => ScheduleTask(txToFetch), cancel));
		}

		async Task ScheduleTask(SmartTransaction transaction)
		{
			var random = new Random();
			var delayInSeconds = random.Next(MaximumDelayInSeconds);
			var delay = TimeSpan.FromSeconds(delayInSeconds);

			try
			{
				await Task.Delay(delay, cancel).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				Logger.LogTrace($"FetchCpfpInfoAsync was unscheduled for {transaction.GetHash()} because Wasabi is shutting down");
				return;
			}

			try
			{
				await FetchCpfpInfoAsync(transaction, cancel).ConfigureAwait(false);
			}
			catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
			{
				if (cancel.IsCancellationRequested)
				{
					Logger.LogTrace($"FetchCpfpInfoAsync was canceled for {transaction.GetHash()} because Wasabi is shutting down");
					return;
				}

				// Reschedule
				if (ShouldRequest(transaction))
				{
					await ScheduleTask(transaction).ConfigureAwait(false);
				}
			}
			finally
			{
				scheduledRequests.Remove(transaction.GetHash());
			}

		}
	}

	public static bool ShouldRequest(SmartTransaction tx)
	{
		return !tx.Confirmed && (tx.ForeignInputs.Count != 0 || tx.ForeignOutputs.Count != 0);
	}

	public void ScheduleRequest(SmartTransaction tx)
	{
		_transactionsChannel.Writer.TryWrite(tx);
	}

	public async Task<CpfpInfo> ImmediateRequestAsync(SmartTransaction tx, CancellationToken cancellationToken)
	{
		return await GetCpfpInfoAsync(tx.GetHash(), cancellationToken).ConfigureAwait(false);
	}

	public async Task UpdateCacheAsync(CancellationToken cancel)
	{
		if (_lastUpdateCacheLoop + MinimumBetweenUpdateRequests > DateTimeOffset.UtcNow)
		{
			return;
		}

		_lastUpdateCacheLoop = DateTimeOffset.UtcNow;

		List<uint256> toRemoveFromCache = [];

		using (await AsyncLock.LockAsync(cancel).ConfigureAwait(false))
		{
			foreach (var cachedCpfpInfo in _cpfpInfoCache)
			{
				if (!ShouldRequest(cachedCpfpInfo.Value.Transaction))
				{
					toRemoveFromCache.Add(cachedCpfpInfo.Key);
					continue;
				}

				ScheduleRequest(cachedCpfpInfo.Value.Transaction);
			}

			foreach (var toRemove in toRemoveFromCache)
			{
				_cpfpInfoCache.Remove(toRemove);
			}
		}
	}

	public async Task<CpfpInfo?> GetCachedCpfpInfoAsync(uint256 txid, CancellationToken cancel)
	{
		using (await AsyncLock.LockAsync(cancel).ConfigureAwait(false))
		{
			return _cpfpInfoCache.TryGetValue(txid, out var cached) ? cached.CpfpInfo : null;
		}
	}

	private async Task FetchCpfpInfoAsync(SmartTransaction transaction, CancellationToken cancellationToken)
	{
		var txid = transaction.GetHash();
		var cpfpInfo = await GetCpfpInfoAsync(txid, cancellationToken).ConfigureAwait(false);
		_cpfpInfoCache.AddOrReplace(txid, new CachedCpfpInfo(cpfpInfo, transaction, DateTimeOffset.UtcNow));

		RequestedCpfpInfoArrived.SafeInvoke(this, EventArgs.Empty);
	}

	private async Task<CpfpInfo> GetCpfpInfoAsync(uint256 txid, CancellationToken cancellationToken)
	{
		using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(20));
		using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

		var httpClient = _httpClientFactory.CreateClient($"mempool.space-{txid}");
		var response = await httpClient.GetAsync( $"v1/cpfp/{txid}", linkedCts.Token).ConfigureAwait(false);

		if (response.StatusCode != HttpStatusCode.OK)
		{
			await response.ThrowRequestExceptionFromContentAsync(cancellationToken).ConfigureAwait(false);
		}

		var stringResponse = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

		return JsonConvert.DeserializeObject<CpfpInfo>(stringResponse, JsonSerializationOptions.Default.Settings) ??
		       throw new JsonException("Deserialization error");;
	}

	private record CachedCpfpInfo(CpfpInfo CpfpInfo, SmartTransaction Transaction, DateTimeOffset TimeLastUpdate);
}
