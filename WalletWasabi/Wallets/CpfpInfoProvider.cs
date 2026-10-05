using NBitcoin;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Serialization;
using WalletWasabi.Services;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Wallets;

public abstract record CpfpInfoMessage
{
	public record UpdateMessage : CpfpInfoMessage;
	public record GetCachedCpfpInfo(IReplyChannel<CachedCpfpInfo[]> ReplyChannel) : CpfpInfoMessage;
	public record PreFetchInfoForTransaction(SmartTransaction SmartTransaction) : CpfpInfoMessage;
	public record GetInfoForTransaction(SmartTransaction SmartTransaction, IReplyChannel<Result<CpfpInfo, string>> ReplyChannel) : CpfpInfoMessage;
}

public record CachedCpfpInfo(CpfpInfo CpfpInfo, SmartTransaction Transaction);

public class CpfpInfoProvider(MailboxProcessor<CpfpInfoMessage> cpfpUpdater)
{
	public Task<CachedCpfpInfo[]> GetCachedCpfpInfoAsync(CancellationToken cancellationToken) =>
		cpfpUpdater.PostAndReplyAsync<CachedCpfpInfo[]>(chan => new CpfpInfoMessage.GetCachedCpfpInfo(chan), cancellationToken);

	public void ScheduleRequest(SmartTransaction tx) =>
		cpfpUpdater.Post(new CpfpInfoMessage.PreFetchInfoForTransaction(tx));

	public Task<Result<CpfpInfo,string>> GetCpfpInfoAsync(SmartTransaction tx, CancellationToken cancellationToken) =>
		cpfpUpdater.PostAndReplyAsync<Result<CpfpInfo,string>>(chan => new CpfpInfoMessage.GetInfoForTransaction(tx, chan), cancellationToken);
}

public static class CpfpInfoUpdater
{
	private delegate Task<Result<CpfpInfo,string>> CpfpInfoGetter(SmartTransaction stx);

	public static MessageHandler<CpfpInfoMessage, Unit> CreateForRegTest()
	{
		return (msg, _, _) =>
		{
			// CPFP is not properly supported in regtest yet.
			switch (msg)
			{
				case CpfpInfoMessage.GetCachedCpfpInfo m:
					m.ReplyChannel.Reply([]);
					break;
				case CpfpInfoMessage.GetInfoForTransaction m:
					m.ReplyChannel.Reply(Result<CpfpInfo, string>.Fail("Not implemented for regtest."));
					break;
			}

			return Task.FromResult(Unit.Instance);
		};
	}

	public static MessageHandler<CpfpInfoMessage, Unit> Create(
		IHttpClientFactory httpClientFactory, Network network, EventBus eventBus)
	{
		var uri = MempoolSpaceApi.GetBaseUri(httpClientFactory, network);
		var tasks = new List<Task>();
		var cache = new ConcurrentDictionary<uint256, CachedCpfpInfo>();
		return (msg, _, cancellationToken) => ProcessMessagesAsync(msg, httpClientFactory, uri, tasks, cache, eventBus, cancellationToken);
	}

	private static async Task<Unit> ProcessMessagesAsync(CpfpInfoMessage msg, IHttpClientFactory httpClientFactory, Uri uri, List<Task> tasks, ConcurrentDictionary<uint256, CachedCpfpInfo> cache, EventBus eventBus, CancellationToken cancellationToken)
	{
		switch (msg)
		{
			case CpfpInfoMessage.UpdateMessage _ :
				await ProcessFinishedFetchingTasksAsync(tasks, cancellationToken).ConfigureAwait(false);
				CleanCache(cache);
				var rescheduledFetchingTasks = RescheduleAll(cache, RefreshCpfpInfo, cancellationToken);
				tasks.AddRange(rescheduledFetchingTasks);
				break;
			case CpfpInfoMessage.GetCachedCpfpInfo m:
				m.ReplyChannel.Reply(cache.Values.ToArray());
				break;
			case CpfpInfoMessage.GetInfoForTransaction m:
				var cpfpInfo = await GetCpfpInfo(m.SmartTransaction).ConfigureAwait(false);
				m.ReplyChannel.Reply(cpfpInfo);
				break;
			case CpfpInfoMessage.PreFetchInfoForTransaction m:
				var scheduledFetchingTask = ScheduleTaskAsync(m.SmartTransaction, GetCpfpInfo, cancellationToken);
				tasks.Add(scheduledFetchingTask);
				break;
		}

		return Unit.Instance;

		Task<Result<CpfpInfo, string>> GetCpfpInfo(SmartTransaction tx) => FetchCpfpInfoAsync(tx, refresh: false);
		Task<Result<CpfpInfo, string>> RefreshCpfpInfo(SmartTransaction tx) => FetchCpfpInfoAsync(tx, refresh: true);

		async Task<Result<CpfpInfo, string>> FetchCpfpInfoAsync(SmartTransaction tx, bool refresh)
		{
			var result = await GetCpfpInfoAsync(tx, httpClientFactory, uri, cache, cancellationToken, refresh).ConfigureAwait(false);
			return result.Map(
				info =>
				{
					eventBus.Publish(new CpfpInfoArrived());
					return info;
				});
		}
	}

	private static async Task ProcessFinishedFetchingTasksAsync(List<Task> tasks, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var completedTasks = tasks.Where(t => t.IsCompleted).ToArray();
		await Task.WhenAll(completedTasks).ConfigureAwait(false);
		tasks.RemoveAll(t => completedTasks.Contains(t));
	}

	private static void CleanCache(ConcurrentDictionary<uint256, CachedCpfpInfo> cache)
	{
		var confirmed = cache.Where(e => e.Value.Transaction.Confirmed).ToArray();

		foreach (var cacheEntry in confirmed)
		{
			cache.TryRemove(cacheEntry.Key, out _);
		}
	}

	private static IEnumerable<Task> RescheduleAll(ConcurrentDictionary<uint256, CachedCpfpInfo> cache, CpfpInfoGetter cpfpGetter, CancellationToken cancellationToken)
	{
		var unconfirmed = cache.Where(e => !e.Value.Transaction.Confirmed).ToArray();

		foreach (var cacheEntry in unconfirmed)
		{
			yield return ScheduleTaskAsync(cacheEntry.Value.Transaction, cpfpGetter, cancellationToken);
		}
	}

	private	static async Task ScheduleTaskAsync(SmartTransaction transaction, CpfpInfoGetter cpfpGetter, CancellationToken cancellationToken)
	{
		if (!transaction.CanBeSpeedUpUsingCpfp())
		{
			return;
		}

		const int MaximumDelayInMilliseconds = 10_000;
		var random = RandomnessProviders.Secure;
		var delayInMilliseconds = random.GetInt(MaximumDelayInMilliseconds);
		var delay = TimeSpan.FromMilliseconds(delayInMilliseconds);

		try
		{
			await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
			await cpfpGetter(transaction).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				Logger.LogTrace($"FetchCpfpInfoAsync was canceled for {transaction.GetHash()} because Wasabi is shutting down");
			}
		}
	}

	private static async Task<Result<CpfpInfo, string>> GetCpfpInfoAsync(SmartTransaction tx, IHttpClientFactory httpClientFactory, Uri uri, ConcurrentDictionary<uint256, CachedCpfpInfo> cache, CancellationToken cancellationToken, bool refresh)
	{
		var txid = tx.GetHash();
		if (!refresh && cache.TryGetValue(txid, out var cachedCpfpInfo))
		{
			return cachedCpfpInfo.CpfpInfo;
		}

		try
		{
			var cpfpInfo = await GetCpfpInfoAsync(txid, httpClientFactory, uri, cancellationToken).ConfigureAwait(false);
			cache[txid] = new CachedCpfpInfo(cpfpInfo, tx);
			return cpfpInfo;
		}
		catch (Exception e)
		{
			return Result<CpfpInfo, string>.Fail(e.Message);
		}
	}

	private static async Task<CpfpInfo> GetCpfpInfoAsync(uint256 txid, IHttpClientFactory httpClientFactory, Uri uri, CancellationToken cancellationToken)
	{
		using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

		using var httpClient = httpClientFactory.CreateClient($"mempool.space-{txid}");
		httpClient.BaseAddress = uri;
		using var request = new HttpRequestMessage(HttpMethod.Get, $"v1/cpfp/{txid}");
		using var response = await httpClient.SendAsync(request, linkedCts.Token).ConfigureAwait(false);

		response.EnsureSuccessStatusCode();

		var stringResponse = await response.Content.ReadAsStringAsync(linkedCts.Token).ConfigureAwait(false);

		return JsonDecoder.FromString(stringResponse, Decode.CpfpInfo)
			?? throw new DataException("Deserialization error");
	}
}
