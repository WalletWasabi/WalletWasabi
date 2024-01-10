using Microsoft.Extensions.Hosting;
using NBitcoin;
using Nito.AsyncEx;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Extensions;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Stores;
using WalletWasabi.Wallets.FilterProcessor;

namespace WalletWasabi.Wallets;

/// <summary>
/// Service that keeps processing block filters.
/// </summary>
/// <seealso href="https://github.com/zkSNACKs/WalletWasabi/issues/10219">TurboSync specification.</seealso>
public class WalletFilterProcessor : BackgroundService
{
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

			return x.Height.CompareTo(y.Height);
		});

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private FilterModel? _lastProcessedFilter;

	public WalletFilterProcessor(KeyManager keyManager, BitcoinStore bitcoinStore, TransactionProcessor transactionProcessor, BlockDownloadService blockDownloadService)
	{
		KeyManager = keyManager;
		BitcoinStore = bitcoinStore;
		TransactionProcessor = transactionProcessor;
		BlockDownloadService = blockDownloadService;

		FilterIteratorsBySyncType = new()
		{
			{ SyncType.Turbo, new BlockFilterIterator(BitcoinStore.IndexStore) },
			{ SyncType.NonTurbo, new BlockFilterIterator(BitcoinStore.IndexStore) },
			{ SyncType.Complete, new BlockFilterIterator(BitcoinStore.IndexStore) },
		};
	}

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private PriorityQueue<SyncRequest, Priority> SynchronizationRequests { get; } = new(Comparer);

	/// <remarks>Guards <see cref="SynchronizationRequests"/> and <see cref="_lastProcessedFilter"/>.</remarks>
	private object Lock { get; } = new();
	private SemaphoreSlim SynchronizationRequestsSemaphore { get; } = new(initialCount: 0);

	private KeyManager KeyManager { get; }
	private BitcoinStore BitcoinStore { get; }
	private TransactionProcessor TransactionProcessor { get; }
	private BlockDownloadService BlockDownloadService { get; }

	public FilterModel? LastProcessedFilter
	{
		get
		{
			lock (Lock)
			{
				return _lastProcessedFilter;
			}
		}
		private set
		{
			lock (Lock)
			{
				_lastProcessedFilter = value;
			}
		}
	}

	/// <remarks>Internal only to allow modifications in tests.</remarks>
	internal Dictionary<SyncType, BlockFilterIterator> FilterIteratorsBySyncType { get; }

	/// <summary>Make sure we don't process any request while a reorg is happening.</summary>
	private AsyncLock ReorgLock { get; } = new();

	public async Task ProcessAsync(IEnumerable<SyncType> syncTypes, CancellationToken cancellationToken)
	{
		var tasks = syncTypes.Select(syncType => ProcessAsync(syncType, cancellationToken)).ToList();
		await Task.WhenAll(tasks).ConfigureAwait(false);
	}

	public async Task ProcessAsync(SyncType syncType, CancellationToken cancellationToken)
	{
		SyncRequest request = new(syncType, new TaskCompletionSource());
		Priority priority = new(request.SyncType);

		lock (Lock)
		{
			SynchronizationRequests.Enqueue(request, priority);
			SynchronizationRequestsSemaphore.Release(releaseCount: 1);
		}

		await request.Tcs.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	/// <summary>Used for filter synchronization.</summary>
	protected override async Task ExecuteAsync(CancellationToken cancellationToken)
	{
		try
		{
			PriorityQueue<Task<FilterPreProcessorResult>, Priority> preProcessTasks = new ();

			while (true)
			{
				await SynchronizationRequestsSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

				SyncRequest? request;

				lock (Lock)
				{
					if (!SynchronizationRequests.TryPeek(out request, out _))
					{
						continue;
					}
				}

				try
				{
					bool reachedBlockChainTip;
					using (await ReorgLock.LockAsync(cancellationToken).ConfigureAwait(false))
					{
						Height lastHeight = (request.SyncType == SyncType.Turbo ? KeyManager.GetBestTurboSyncHeight() : KeyManager.GetBestHeight());

						if (lastHeight == BitcoinStore.SmartHeaderChain.TipHeight)
						{
							request.Tcs.SetResult();
							lock (Lock)
							{
								SynchronizationRequests.Dequeue();
							}
							continue;
						}

						uint toProcessHeight = (uint)lastHeight.Value + 1;

						// MaxNumberFiltersInMemory is also the maximum of pre-process slots because filters are in memory until the end of the task.
						// This is not 100% true because the filter has to stay in memory until after ProcessFilterModelAsync finishes.
						var availablePreProcessSlots = Math.Max(0, FilterIteratorsBySyncType[request.SyncType].MaxNumberFiltersInMemory - preProcessTasks.Count);

						// We have to skip all the current tasks.
						// TODO: Improve performances by keeping track of the count for each SyncType
						var toPreProcessHeight = toProcessHeight + (uint)preProcessTasks.UnorderedItems.Count(x => x.Priority.SyncType == request.SyncType);
						for (uint i = 0; i < availablePreProcessSlots; i++)
						{
							var filter = await FilterIteratorsBySyncType[request.SyncType].GetAndRemoveAsync(toPreProcessHeight, cancellationToken).ConfigureAwait(false);
							preProcessTasks.Enqueue(PreProcessFilterModelAsync(filter, request.SyncType, cancellationToken), new Priority(request.SyncType, toPreProcessHeight));

							toPreProcessHeight++;
							if (toPreProcessHeight > BitcoinStore.SmartHeaderChain.TipHeight)
							{
								break;
							}
						}

						// TODO: Reorgs??

						var preProcessResult = await preProcessTasks.Dequeue().ConfigureAwait(false);
						var matchFound = await ProcessFilterModelAsync(preProcessResult, request.SyncType, cancellationToken).ConfigureAwait(false);

						reachedBlockChainTip = toProcessHeight == BitcoinStore.SmartHeaderChain.TipHeight;
						bool storeToDisk = matchFound || reachedBlockChainTip;
						KeyManager.SetBestHeight(request.SyncType, new Height(toProcessHeight), storeToDisk);
					}

					if (reachedBlockChainTip)
					{
						request.Tcs.SetResult();
						lock (Lock)
						{
							SynchronizationRequests.Dequeue();
						}
					}
					else
					{
						SynchronizationRequestsSemaphore.Release(1);
					}
				}
				catch (Exception ex)
				{
					if (!request.Tcs.TrySetException(ex))
					{
						Logger.LogWarning($"Tried to set exception for {request.SyncType.FriendlyName()} but status was already {request.Tcs.Task.Status}.");
					}

					throw;
				}

				// Clear all caches once not needed.
				if (SynchronizationRequestsSemaphore.CurrentCount == 0)
				{
					foreach (SyncType syncType in Enum.GetValues<SyncType>())
					{
						FilterIteratorsBySyncType[syncType].Clear();
					}
				}
			}
		}
		catch (OperationCanceledException)
		{
			Logger.LogDebug("Filter processor's execution was stopped.");
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			throw;
		}
		finally
		{
			lock (Lock)
			{
				while (SynchronizationRequests.TryDequeue(out var request, out _))
				{
					_ = request.Tcs.TrySetCanceled(cancellationToken);
				}
			}
		}
	}

	/// <summary>
	/// Return the keys to test against the filter depending on the height of the filter and the type of synchronization.
	/// </summary>
	/// <param name="filterHeight">Height of the filter that needs to be tested.</param>
	/// <param name="syncType">First sync of TurboSync, second one, or complete synchronization.</param>
	/// <returns>Keys to test against this filter.</returns>
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

	private async Task<FilterPreProcessorResult> PreProcessFilterModelAsync(FilterModel filter, SyncType syncType, CancellationToken cancel)
	{
		var height = new Height(filter.Header.Height);
		var toTestKeys = GetScriptPubKeysToTest(height, syncType);
		var match = toTestKeys.Count != 0 && filter.Filter.MatchAny(toTestKeys, filter.FilterKey);
		if (match)
		{
			await BlockDownloadService.Enqueue(filter.Header.BlockHash, filter.Header.Height, syncType).Task;

			// The block is now available on the disk, it will be accessed by ProcessFilterModel to avoid caching.
		}
		return new FilterPreProcessorResult(filter, match);
	}

	private async Task<bool> ProcessFilterModelAsync(FilterPreProcessorResult preProcessorResult, SyncType syncType, CancellationToken cancel)
	{
		var filter = preProcessorResult.Filter;
		var height = new Height(filter.Header.Height);

		var matchFound = false;
		if (preProcessorResult.Match)
		{
			matchFound = true;
		}
		else
		{
			// Test against the keys that haven't been used for the preprocessing
			// TODO: Don't retest against keys that were tested during preprocessing.
			var toTestKeys = GetScriptPubKeysToTest(height, syncType);

			if (toTestKeys.Count > 0)
			{
				matchFound = filter.Filter.MatchAny(toTestKeys, filter.FilterKey);
			}
		}

		if (matchFound)
		{
			Block currentBlock = await BlockDownloadService.BlockProvider.GetBlockAsync(filter.Header.BlockHash, cancel);

			var txsToProcess = new List<SmartTransaction>();
			for (int i = 0; i < currentBlock.Transactions.Count; i++)
			{
				Transaction tx = currentBlock.Transactions[i];
				txsToProcess.Add(new SmartTransaction(tx, height, currentBlock.GetHash(), i, firstSeen: currentBlock.Header.BlockTime, labels: BitcoinStore.MempoolService.TryGetLabel(tx.GetHash())));
			}

			TransactionProcessor.Process(txsToProcess);
		}

		LastProcessedFilter = filter;
		return matchFound;
	}

	private async void ReorgedAsync(object? sender, FilterModel invalidFilter)
	{
		try
		{
			uint256 invalidBlockHash = invalidFilter.Header.BlockHash;

			using (await ReorgLock.LockAsync(CancellationToken.None).ConfigureAwait(false))
			{
				KeyManager.SetMaxBestHeight(new Height(invalidFilter.Header.Height - 1));
				TransactionProcessor.UndoBlock((int)invalidFilter.Header.Height);
				BitcoinStore.TransactionStore.ReleaseToMempoolFromBlock(invalidBlockHash);
				// TODO: Call correctly BlockDownloadService.Remove
				if (BlockDownloadService.BlockProvider is SmartBlockProvider smartBlockProvider)
				{
					await smartBlockProvider.RemoveAsync(invalidBlockHash, CancellationToken.None).ConfigureAwait(false);
				}
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
		await base.StopAsync(cancellationToken).ConfigureAwait(false);
	}

	public override void Dispose()
	{
		SynchronizationRequestsSemaphore.Dispose();
		base.Dispose();
	}

	public record SyncRequest(SyncType SyncType, TaskCompletionSource Tcs);
	public record Priority(SyncType SyncType, uint Height = 0);
	private record FilterPreProcessorResult(FilterModel Filter, bool Match);
}
