using NBitcoin;
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
		cpfpUpdater
			.PostAndReplyAsync<CachedCpfpInfo[]>(chan => new CpfpInfoMessage.GetCachedCpfpInfo(chan),
				cancellationToken);

	public void ScheduleRequest(SmartTransaction tx) =>
		cpfpUpdater.Post(new CpfpInfoMessage.PreFetchInfoForTransaction(tx));

	public Task<Result<CpfpInfo,string>> GetCpfpInfoAsync(SmartTransaction tx, CancellationToken cancellationToken) =>
		cpfpUpdater.PostAndReplyAsync<Result<CpfpInfo,string>>(chan => new CpfpInfoMessage.GetInfoForTransaction(tx, chan), cancellationToken);
}

public static class CpfpInfoUpdater
{
	private delegate Task<Result<CpfpInfo,string>> CpfpInfoGetter(SmartTransaction stx);

	public static MessageHandler<CpfpInfoMessage, Unit> CreateForRegTest() =>
		(_, _, _) => Task.FromResult(Unit.Instance);

	public static MessageHandler<CpfpInfoMessage, Unit> Create(
		IHttpClientFactory httpClientFactory, Network network, EventBus eventBus)
	{
		var uri = network == Network.Main
			? new Uri("https://mempool.space/api/")
			: new Uri("https://mempool.space/testnet/api/");
		var tasks = new List<Task>();
		var cache = new Dictionary<uint256, CachedCpfpInfo>();
		return (msg, _, cancellationToken) => ProcessMessagesAsync(msg, httpClientFactory, uri, tasks, cache, eventBus, cancellationToken);
	}

	private static async Task<Unit> ProcessMessagesAsync(CpfpInfoMessage msg, IHttpClientFactory httpClientFactory, Uri uri, List<Task> tasks, Dictionary<uint256, CachedCpfpInfo> cache, EventBus eventBus, CancellationToken cancellationToken)
	{
		switch (msg)
		{
			case CpfpInfoMessage.UpdateMessage _ :
				await ProcessFinishedFetchingTasksAsync(tasks, cancellationToken).ConfigureAwait(false);
				CleanCache(cache);
				var rescheduledFetchingTasks = RescheduleAll(cache, GetCpfpInfo, cancellationToken);
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

		async Task<Result<CpfpInfo, string>> GetCpfpInfo(SmartTransaction tx)
		{
			var result = await GetCpfpInfoAsync(tx, httpClientFactory, uri, cache, cancellationToken).ConfigureAwait(false);
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
		var completedTasks = tasks.Where(t => t.IsCompleted).ToArray();
		await Task.WhenAll(completedTasks).ConfigureAwait(false);
		tasks.RemoveAll(t => completedTasks.Contains(t));
	}

	private static void CleanCache(Dictionary<uint256, CachedCpfpInfo> cache)
	{
		var confirmed = cache.Where(e => e.Value.Transaction.Confirmed).ToArray();

		foreach (var cacheEntry in confirmed)
		{
			cache.Remove(cacheEntry.Key);
		}
	}

	private static IEnumerable<Task> RescheduleAll(Dictionary<uint256, CachedCpfpInfo> cache, CpfpInfoGetter cpfpGetter, CancellationToken cancellationToken)
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
		var random = SecureRandom.Instance;
		var delayInSeconds = random.GetInt(0, MaximumDelayInMilliseconds);
		var delay = TimeSpan.FromMilliseconds(delayInSeconds);

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

	private static async Task<Result<CpfpInfo, string>> GetCpfpInfoAsync(SmartTransaction tx, IHttpClientFactory httpClientFactory, Uri uri, Dictionary<uint256, CachedCpfpInfo> cache, CancellationToken cancellationToken)
	{
		var txid = tx.GetHash();
		if (cache.TryGetValue(txid, out var cachedCpfpInfo))
		{
			return cachedCpfpInfo.CpfpInfo;
		}

		try
		{
			var cpfpInfo = await GetCpfpInfoAsync(txid, httpClientFactory, uri, cancellationToken).ConfigureAwait(false);
			cache.Add(txid, new CachedCpfpInfo(cpfpInfo, tx));
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

		var httpClient = httpClientFactory.CreateClient($"mempool.space-{txid}");
		httpClient.BaseAddress = uri;
		var response = await httpClient.GetAsync( $"v1/cpfp/{txid}", linkedCts.Token).ConfigureAwait(false);

		response.EnsureSuccessStatusCode();

		var stringResponse = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

		return JsonDecoder.FromString(stringResponse, Decode.CpfpInfo) ??
		       throw new DataException("Deserialization error");;
	}
}
