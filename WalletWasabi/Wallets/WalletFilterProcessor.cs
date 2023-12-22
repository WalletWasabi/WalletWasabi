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

namespace WalletWasabi.Wallets;

/// <summary>
/// Service that keeps processing block filters.
/// </summary>
/// <seealso href="https://github.com/zkSNACKs/WalletWasabi/issues/10219">TurboSync specification.</seealso>
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

			return 0;
		});

	public WalletFilterProcessor(KeyManager keyManager, BitcoinStore bitcoinStore, TransactionProcessor transactionProcessor, IBlockProvider blockProvider)
	{
		KeyManager = keyManager;
		BitcoinStore = bitcoinStore;
		TransactionProcessor = transactionProcessor;
		BlockProvider = blockProvider;
	}

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private PriorityQueue<SyncRequest, Priority> SynchronizationRequests { get; } = new(Comparer);

	/// <remarks>Guards <see cref="SynchronizationRequests"/>.</remarks>
	private object Lock { get; } = new();
	private SemaphoreSlim SynchronizationRequestsSemaphore { get; } = new(initialCount: 0);

	private KeyManager KeyManager { get; }
	private BitcoinStore BitcoinStore { get; }
	private TransactionProcessor TransactionProcessor { get; }
	private IBlockProvider BlockProvider { get; }
	public FilterModel? LastProcessedFilter { get; private set; }

	/// <remarks>Internal only to allow modifications in tests.</remarks>
	internal Dictionary<uint, FilterModel> FiltersCache { get; } = new();

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
						var currentHeight = (request.SyncType == SyncType.Turbo ? KeyManager.GetBestTurboSyncHeight() : KeyManager.GetBestHeight());

						if (currentHeight == BitcoinStore.SmartHeaderChain.TipHeight)
						{
							request.Tcs.SetResult();
							lock (Lock)
							{
								SynchronizationRequests.Dequeue();
							}
							continue;
						}

						var heightToTest = (uint)currentHeight.Value + 1;
						if (!FiltersCache.TryGetValue(heightToTest, out var filterToProcess))
						{
							filterToProcess = await UpdateFiltersCacheAndReturnFirstAsync(heightToTest, cancellationToken).ConfigureAwait(false);
						}

						var matchFound = await ProcessFilterModelAsync(filterToProcess, request.SyncType, cancellationToken).ConfigureAwait(false);

						reachedBlockChainTip = filterToProcess.Header.Height == BitcoinStore.SmartHeaderChain.TipHeight;
						var saveNewHeightToFile = matchFound || reachedBlockChainTip;
						if (request.SyncType == SyncType.Turbo)
						{
							// Only keys in TurboSync subset (external + internal that didn't receive or fully spent coins) were tested, update TurboSyncHeight
							KeyManager.SetBestTurboSyncHeight(new Height(filterToProcess.Header.Height), saveNewHeightToFile);
						}
						else
						{
							// All keys were tested at this height, update the Height.
							KeyManager.SetBestHeight(new Height(filterToProcess.Header.Height), saveNewHeightToFile);
						}
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

				if (SynchronizationRequestsSemaphore.CurrentCount == 0)
				{
					FiltersCache.Clear();
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
			FiltersCache.Clear();
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

			using (await ReorgLock.LockAsync(CancellationToken.None).ConfigureAwait(false))
			{
				KeyManager.SetMaxBestHeight(new Height(invalidFilter.Header.Height - 1));
				TransactionProcessor.UndoBlock((int)invalidFilter.Header.Height);
				BitcoinStore.TransactionStore.ReleaseToMempoolFromBlock(invalidBlockHash);

				if (BlockProvider is SmartBlockProvider smartBlockProvider)
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
	public record Priority(SyncType SyncType);
}
