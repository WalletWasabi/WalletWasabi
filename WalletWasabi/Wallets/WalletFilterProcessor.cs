using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using NBitcoin;
using WalletWasabi.Backend.Models;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Stores;

namespace WalletWasabi.Wallets;

public class WalletFilterProcessor : BackgroundService
{
	private const int MaxNumberFiltersInMemory = 1000;
	
	public static readonly Comparer<Priority> Comparer = Comparer<Priority>.Create(
		(x, y) =>
		{
			// Turbo and Complete have higher priority over NonTurbo.
			if (x.SyncType != SyncType.NonTurbo && y.SyncType == SyncType.NonTurbo)
            {
            	return -1;
            }
			if (y.SyncType != SyncType.NonTurbo && x.SyncType == SyncType.NonTurbo)
			{
				return 1;
			}

			// Higher height have higher priority.
			return x.Height.CompareTo(y.Height);
		});
	
	public WalletFilterProcessor(KeyManager keyManager, BitcoinStore bitcoinStore, TransactionProcessor transactionProcessor, IBlockProvider blockProvider)
	{
		KeyManager = keyManager;
		BitcoinStore = bitcoinStore;
		TransactionProcessor = transactionProcessor;
		BlockProvider = blockProvider;
	}

	private PriorityQueue<SyncRequest, Priority> SynchronizationRequests { get; } = new(Comparer);
	private SemaphoreSlim SynchronizationRequestsSemaphore { get; } = new(initialCount: 0);

	/// <remarks>Guards <see cref="SynchronizationRequests"/>.</remarks>
	private object SynchronizationRequestsLock { get; } = new();

	private KeyManager KeyManager { get; }
	private BitcoinStore BitcoinStore { get; }
	private TransactionProcessor TransactionProcessor { get; }
	private IBlockProvider BlockProvider { get; }
	public FilterModel? LastProcessedFilter { get; private set; }
	private Dictionary<uint, FilterModel> FiltersCache { get; } = new ();

	/// <remarks>Guarded by <see cref="SynchronizationRequestsLock"/>.</remarks>
	private void AddNoLock(SyncRequest request)
	{
		Priority priority = new(request.SyncType, request.Height);
		SynchronizationRequests.Enqueue(request, priority);
		SynchronizationRequestsSemaphore.Release(releaseCount: 1);
	}

	/// <remarks>Guarded by <see cref="SynchronizationRequestsLock"/>.</remarks>
	private void InvalidateRequestsNoLock(uint fromHeight)
	{
		SynchronizationRequests.UnorderedItems
			.Where(item => item.Element.Height >= fromHeight)
			.ToList()
			.ForEach(item => item.Element.DoNotProcess = true);
	}

	public async Task ProcessAsync(uint toHeight, IEnumerable<SyncType> syncTypes, CancellationToken cancellationToken)
	{
		var tasks = syncTypes.Select(syncType => ProcessAsync(toHeight, syncType, cancellationToken)).ToList();
		await Task.WhenAll(tasks).ConfigureAwait(false);
	}
	
	public async Task ProcessAsync(uint toHeight, SyncType syncType, CancellationToken cancellationToken)
	{
		List<Task> tasks = new();
		lock (SynchronizationRequestsLock)
		{
			uint startingHeight;
			var existingRequestsForSyncType = SynchronizationRequests.UnorderedItems.Where(x => x.Element.SyncType == syncType).ToList();
			if (existingRequestsForSyncType.Count > 0)
			{
				startingHeight = existingRequestsForSyncType.Max(x => x.Element.Height) + 1;
			}
			else
			{
				startingHeight = (uint)(syncType == SyncType.Turbo ? 
					KeyManager.GetBestTurboSyncHeight() + 1:
					KeyManager.GetBestHeight() + 1);
			}

			if (toHeight >= startingHeight)
			{
				foreach (var height in Enumerable.Range((int)startingHeight, (int)(toHeight - startingHeight) + 1))
				{
					var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
					cancellationToken.Register(() => tcs.TrySetCanceled());
					AddNoLock(new SyncRequest(syncType, (uint)height, tcs));
				}
			}
			
			// We have to wait for all the requests below toHeight to be processed.
			tasks.AddRange(SynchronizationRequests.UnorderedItems.Where(x => x.Element.SyncType == syncType && x.Element.Height <= toHeight).Select(x => x.Element.Tcs.Task));
		}

		await Task.WhenAll(tasks).ConfigureAwait(false); // This will throw if a tasks throws.
	}

	/// <inheritdoc />
	/// <summary>Used for filter synchronization.</summary>
	protected override async Task ExecuteAsync(CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested)
		{
			await SynchronizationRequestsSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

			SyncRequest? request;
			lock (SynchronizationRequestsLock)
			{
				if (!SynchronizationRequests.TryDequeue(out request, out _))
				{
					continue;
				}
			}

			if (request.DoNotProcess)
			{
				request.Tcs.SetCanceled(CancellationToken.None);
				continue;
			}

			// Cancel every request if one is canceled to avoid synchronization problems when different cancellation tokens are used.
			if (request.Tcs.Task.IsCanceled)
			{
				CancelEveryRequest();
				continue;
			}
			
			try
			{
				if (!FiltersCache.TryGetValue(request.Height, out var filterToProcess))
				{
					filterToProcess = await UpdateFiltersCacheAndReturnFirstAsync(request.Height, cancellationToken).ConfigureAwait(false);
				}

				var matchFound = await ProcessFilterModelAsync(filterToProcess, request.SyncType, cancellationToken).ConfigureAwait(false);

				if (request.SyncType == SyncType.Turbo)
				{
					// Only keys in TurboSync subset (external + internal that didn't receive or fully spent coins) were tested, update TurboSyncHeight
					KeyManager.SetBestTurboSyncHeight(new Height(filterToProcess.Header.Height), (matchFound || filterToProcess.Header.Height == BitcoinStore.IndexStore.SmartHeaderChain.TipHeight));
				}
				else
				{
					// All keys were tested at this height, update the Height.
					KeyManager.SetBestHeight(new Height(filterToProcess.Header.Height), (matchFound || filterToProcess.Header.Height == BitcoinStore.IndexStore.SmartHeaderChain.TipHeight));
				}
				
				request.Tcs.SetResult();
			}
			catch (Exception ex)
			{
				if (ex is not OperationCanceledException)
				{
					Logger.LogError(ex);
				}

				request.Tcs.SetException(ex);
				CancelEveryRequest();
				throw;
			}

			if (SynchronizationRequestsSemaphore.CurrentCount == 0)
			{
				FiltersCache.Clear();
			}
		}
	}

	private void CancelEveryRequest()
	{
		lock (SynchronizationRequestsLock)
		{
			// Cancel the remaining tasks before throwing.
			while (SynchronizationRequests.TryDequeue(out var request, out _))
			{
				request.Tcs.SetCanceled(CancellationToken.None);
			}
		}
	}

	private async Task<FilterModel> UpdateFiltersCacheAndReturnFirstAsync(uint startingHeight, CancellationToken cancellationToken)
	{
		FiltersCache.Clear();
		var filtersBatch = await BitcoinStore.IndexStore.FetchBatchAsync(new Height(startingHeight), MaxNumberFiltersInMemory, cancellationToken).ConfigureAwait(false);
		foreach (var filter in filtersBatch)
		{
			FiltersCache[filter.Header.Height] = filter;
		}

		return filtersBatch.First();
	}
	
	public void AddToCache(IEnumerable<FilterModel> filters)
	{
		foreach (var filter in filters)
		{
			FiltersCache[filter.Header.Height] = filter;
		}
	}
	
	/// <summary>
	/// Return the keys to test against the filter depending on the height of the filter and the type of synchronization.
	/// </summary>
	/// <param name="filterHeight">Height of the filter that needs to be tested.</param>
	/// <param name="syncType">First sync of TurboSync, second one, or complete synchronization.</param>
	/// <returns>Keys to test against this filter</returns>
	/// <seealso href="https://github.com/zkSNACKs/WalletWasabi/issues/10219">TurboSync specification.</seealso>
	private List<byte[]> GetScriptPubKeysToTest(Height filterHeight, SyncType syncType)
	{
		if (syncType == SyncType.Complete)
		{
			return KeyManager.UnsafeGetSynchronizationInfos().Select(x => x.ScriptBytesHdPubKeyPair.ScriptBytes).ToList();
		}

		Func<HdPubKey, bool> stepPredicate = syncType == SyncType.Turbo
			? hdPubKey => hdPubKey.LatestSpendingHeight is null || (Height)hdPubKey.LatestSpendingHeight >= filterHeight
			: hdPubKey => hdPubKey.LatestSpendingHeight is not null && (Height)hdPubKey.LatestSpendingHeight < filterHeight;

		IEnumerable<byte[]> keysToTest = KeyManager.UnsafeGetSynchronizationInfos()
			.Where(x => stepPredicate(x.ScriptBytesHdPubKeyPair.HdPubKey))
			.Select(x => x.ScriptBytesHdPubKeyPair.ScriptBytes);

		return keysToTest.ToList();
	}

	private async Task<bool> ProcessFilterModelAsync(FilterModel filter, SyncType syncType, CancellationToken cancel)
	{
		var height = new Height(filter.Header.Height);
		var toTestKeys = GetScriptPubKeysToTest(height, syncType);

		var matchFound = false;
		if (toTestKeys.Count > 0)
		{
			matchFound = filter.Filter.MatchAny(toTestKeys, filter.FilterKey);

			if (matchFound)
			{
				Block currentBlock = await BlockProvider.GetBlockAsync(filter.Header.BlockHash, cancel).ConfigureAwait(false); // Wait until not downloaded.

				var txsToProcess = new List<SmartTransaction>();
				for (int i = 0; i < currentBlock.Transactions.Count; i++)
				{
					Transaction tx = currentBlock.Transactions[i];
					txsToProcess.Add(new SmartTransaction(tx, height, currentBlock.GetHash(), i, firstSeen: currentBlock.Header.BlockTime, labels: BitcoinStore.MempoolService.TryGetLabel(tx.GetHash())));
				}

				TransactionProcessor.Process(txsToProcess);
			}
		}

		LastProcessedFilter = filter;
		return matchFound;
	}
	
	private async void ReorgedAsync(object? sender, FilterModel invalidFilter)
	{
		try
		{
			uint256 invalidBlockHash = invalidFilter.Header.BlockHash;

			lock (SynchronizationRequestsLock)
			{
				InvalidateRequestsNoLock(invalidFilter.Header.Height);
				KeyManager.SetMaxBestHeight(new Height(invalidFilter.Header.Height - 1));
				TransactionProcessor.UndoBlock((int)invalidFilter.Header.Height);
				BitcoinStore.TransactionStore.ReleaseToMempoolFromBlock(invalidBlockHash);
			}
			
			if (BlockProvider is SmartBlockProvider smartBlockProvider)
			{
				await smartBlockProvider.RemoveAsync(invalidBlockHash, CancellationToken.None).ConfigureAwait(false);
			}
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex);
		}
	}

	public override async Task StartAsync(CancellationToken cancellationToken)
	{
		BitcoinStore.IndexStore.Reorged += ReorgedAsync;
		await base.StartAsync(cancellationToken).ConfigureAwait(false);
	}
	
	public override async Task StopAsync(CancellationToken cancellationToken)
	{
		BitcoinStore.IndexStore.Reorged -= ReorgedAsync;
		SynchronizationRequestsSemaphore.Dispose();
		await base.StopAsync(cancellationToken).ConfigureAwait(false);
	}

	public record SyncRequest(SyncType SyncType, uint Height, TaskCompletionSource Tcs)
	{
		public bool DoNotProcess { get; set; }
	}
	public record Priority(SyncType SyncType, uint Height);
}