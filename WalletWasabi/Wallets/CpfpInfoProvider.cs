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
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Http.Extensions;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Wallets;

public class CpfpInfoProvider : BackgroundService
{
	private const int MaximumDelayInSeconds = 120;
	private const int MaximumRequestsInParallel = 3;
	private static readonly TimeSpan MinimumBetweenUpdateRequests = TimeSpan.FromMinutes(2);

	public CpfpInfoProvider(WasabiHttpClientFactory httpClientFactory, Network network)
	{
		if (network == Network.Main)
		{
			HttpClient = httpClientFactory.NewHttpClient(() => new Uri("https://mempool.space/api/"), Tor.Socks5.Pool.Circuits.Mode.NewCircuitPerRequest);
		}
		else if(network == Network.TestNet)
		{
			HttpClient = httpClientFactory.NewHttpClient(() => new Uri("https://mempool.space/testnet/api/"), Tor.Socks5.Pool.Circuits.Mode.NewCircuitPerRequest);
		}
		else
		{
			throw new InvalidOperationException("CpfpInfoProvider is only operational on Main or TestNet");
		}
	}

	public event EventHandler<EventArgs>? RequestedCpfpInfoArrived;

	private ConcurrentDictionary<uint256, CachedCpfpInfo> CpfpInfoCache { get; } = new();

	private IHttpClient HttpClient { get; }

	private Channel<SmartTransaction> Channel { get; } = System.Threading.Channels.Channel.CreateUnbounded<SmartTransaction>();

	private DateTime LastUpdateRequest { get; set; } = DateTime.MinValue;
	private List<uint256> UpdateRequested { get; } = [];

	private async Task FetchCpfpInfoAsync(SmartTransaction transaction, CancellationToken cancellationToken)
	{
		var txid = transaction.GetHash();

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

			var cpfpInfo = await response.Content.ReadAsJsonAsync<CpfpInfo>().ConfigureAwait(false);

			CpfpInfoCache.AddOrReplace(txid, new CachedCpfpInfo(cpfpInfo, transaction));

			RequestedCpfpInfoArrived?.Invoke(this, EventArgs.Empty);
		}
		catch (OperationCanceledException)
		{
			Logger.LogTrace("Request was cancelled by exiting the app.");
		}
	}

	private bool ShouldRequest(SmartTransaction tx, bool ignoreCache = false)
	{
		if (tx.Confirmed || (tx.ForeignInputs.Count == 0 && tx.GetInputs().All(x => x.Confirmed.GetValueOrDefault())))
		{
			return false;
		}

		if (ignoreCache)
		{
			return true;
		}

		return !CpfpInfoCache.ContainsKey(tx.GetHash());
	}

	public void ScheduleRequest(SmartTransaction tx, bool ignoreCache = false)
	{
		if (!ShouldRequest(tx, ignoreCache))
		{
			return;
		}
		Channel.Writer.TryWrite(tx);
	}

	public async Task<CpfpInfo> ImmediateRequestAsync(SmartTransaction tx, CancellationToken cancellationToken)
	{
		if (!ShouldRequest(tx, ignoreCache: true))
		{
			throw new InvalidOperationException($"There is no need to request cpfp info for transaction {tx.GetHash()}");
		}
		await FetchCpfpInfoAsync(tx, cancellationToken).ConfigureAwait(false);
		return CpfpInfoCache[tx.GetHash()].CpfpInfo;
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

				await FetchCpfpInfoAsync(transaction, cancel).ConfigureAwait(false);

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

		var snapshot = CpfpInfoCache.ToList();
		foreach (var cachedCpfpInfo in snapshot.Where(x => !UpdateRequested.Contains(x.Key)))
		{
			UpdateRequested.Add(cachedCpfpInfo.Key);
			ScheduleRequest(cachedCpfpInfo.Value.Transaction, ignoreCache: true);
		}
	}

	public bool TryGetCpfpInfo(uint256 txid, [NotNullWhen(true)] out CpfpInfo? cpfpInfo)
	{
		if (CpfpInfoCache.TryGetValue(txid, out var cached))
		{
			cpfpInfo = cached.CpfpInfo;
			return true;
		}

		cpfpInfo = null;
		return false;
	}

	private record CachedCpfpInfo(CpfpInfo CpfpInfo, SmartTransaction Transaction);
}
