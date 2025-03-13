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
using WalletWasabi.Services.Terminate;
using WalletWasabi.Stores;
using WalletWasabi.Wallets.BlockProvider;
using WalletWasabi.Wallets.FilterProcessor;

namespace WalletWasabi.Wallets;

/// <summary>
/// Service that keeps processing block filters.
/// </summary>
/// <seealso href="https://github.com/WalletWasabi/WalletWasabi/issues/10219">TurboSync specification.</seealso>
public class WalletFilterProcessor : BackgroundService
{
	/// <remarks>Guarded by <see cref="_lock"/>.</remarks>
	private FilterModel? _lastProcessedFilter;

	public WalletFilterProcessor(KeyManager keyManager, BitcoinStore bitcoinStore, TransactionProcessor transactionProcessor, BlockDownloadService blockDownloadService)
	{
		_keyManager = keyManager;
		_bitcoinStore = bitcoinStore;
		_transactionProcessor = transactionProcessor;
		_blockDownloadService = blockDownloadService;

		FilterIteratorsBySyncType = new()
		{
			{ SyncType.Turbo, new BlockFilterIterator(_bitcoinStore.IndexStore) },
			{ SyncType.NonTurbo, new BlockFilterIterator(_bitcoinStore.IndexStore) },
			{ SyncType.Complete, new BlockFilterIterator(_bitcoinStore.IndexStore) },
		};
	}

	/// <remarks>Guarded by <see cref="_lock"/>.</remarks>
	private PriorityQueue<SyncRequest, Priority> SynchronizationRequests { get; } = new(Priority.Comparer);

	/// <remarks>Guards <see cref="SynchronizationRequests"/> and <see cref="_lastProcessedFilter"/>.</remarks>
	private readonly object _lock = new();

	private readonly SemaphoreSlim _synchronizationRequestsSemaphore = new(initialCount: 0);

	private readonly KeyManager _keyManager;
	private readonly BitcoinStore _bitcoinStore;
	private readonly TransactionProcessor _transactionProcessor;
	private readonly BlockDownloadService _blockDownloadService;

	public FilterModel? LastProcessedFilter
	{
		get
		{
			lock (_lock)
			{
				return _lastProcessedFilter;
			}
		}
		set
		{
			lock (_lock)
			{
				_lastProcessedFilter = value;
			}
		}
	}

	/// <remarks>Internal only to allow modifications in tests.</remarks>
	internal Dictionary<SyncType, BlockFilterIterator> FilterIteratorsBySyncType { get; }

	/// <summary>Make sure we don't process any request while a reorg is happening.</summary>
	private readonly AsyncLock _reorgLock = new();

	public async Task ProcessAsync(IEnumerable<SyncType> syncTypes, CancellationToken cancellationToken)
	{
		var tasks = syncTypes.Select(syncType => ProcessAsync(syncType, cancellationToken)).ToList();
		await Task.WhenAll(tasks).ConfigureAwait(false);
	}

	public async Task ProcessAsync(SyncType syncType, CancellationToken cancellationToken)
	{
		SyncRequest request = new(syncType, new TaskCompletionSource());
		Priority priority = new(request.SyncType);

		lock (_lock)
		{
			SynchronizationRequests.Enqueue(request, priority);
			_synchronizationRequestsSemaphore.Release(releaseCount: 1);
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
				await _synchronizationRequestsSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

				SyncRequest? request;

				lock (_lock)
				{
					if (!SynchronizationRequests.TryPeek(out request, out _))
					{
						continue;
					}
				}

				try
				{
					bool reachedBlockChainTip;
					using (await _reorgLock.LockAsync(cancellationToken).ConfigureAwait(false))
					{
						Height lastHeight = _keyManager.GetBestHeight(request.SyncType);

						if (lastHeight == _bitcoinStore.SmartHeaderChain.TipHeight)
						{
							lock (_lock)
							{
								request.Tcs.SetResult();
								SynchronizationRequests.Dequeue();
							}
							continue;
						}

						uint currentHeight = (uint)lastHeight.Value + 1;

						FilterModel filter = await FilterIteratorsBySyncType[request.SyncType].GetAndRemoveAsync(currentHeight, cancellationToken).ConfigureAwait(false);
						var matchFound = await ProcessFilterModelAsync(filter, request.SyncType, cancellationToken).ConfigureAwait(false);

						reachedBlockChainTip = currentHeight == _bitcoinStore.SmartHeaderChain.TipHeight;
						bool storeToDisk = matchFound || reachedBlockChainTip;
						_keyManager.SetBestHeight(request.SyncType, new Height(currentHeight), storeToDisk);
					}

					if (reachedBlockChainTip)
					{
						lock (_lock)
						{
							request.Tcs.SetResult();
							SynchronizationRequests.Dequeue();
						}
					}
					else
					{
						_synchronizationRequestsSemaphore.Release(1);
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
				if (_synchronizationRequestsSemaphore.CurrentCount == 0)
				{
					foreach (SyncType syncType in Enum.GetValues<SyncType>())
					{
						await FilterIteratorsBySyncType[syncType].ClearAsync(cancellationToken).ConfigureAwait(false);
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
			TerminateService.Instance?.SignalGracefulCrash(ex);
			throw;
		}
		finally
		{
			lock (_lock)
			{
				while (SynchronizationRequests.TryDequeue(out var request, out _))
				{
					request.Tcs.TrySetCanceled(cancellationToken);
				}
			}
		}
	}

	/// <summary>
	/// Return the keys to test against the filter depending on the height of the filter and the type of synchronization.
	/// </summary>
	/// <param name="filterHeight">Height of the filter that needs to be tested.</param>
	/// <param name="syncType">First sync of TurboSync, second one, or complete synchronization.</param>
	/// <param name="isBip158"></param>
	/// <returns>Keys to test against this filter.</returns>
	private IEnumerable<byte[]> GetScriptPubKeysToTest(Height filterHeight, SyncType syncType, bool isBip158)
	{
		bool ScriptAlreadySpent(KeyManager.ScriptPubKeySpendingInfo spendingInfo) =>
			spendingInfo.LatestSpendingHeight is { } spendingHeight && spendingHeight < filterHeight;

		bool ScriptNotSpentAtTheMoment(KeyManager.ScriptPubKeySpendingInfo spendingInfo) =>
			!ScriptAlreadySpent(spendingInfo);

		// Wasabi doesn't build bip158 filters and also uses the compact representation of the scriptPubKeys
		var scriptsSpendingInfo = _keyManager.UnsafeGetSynchronizationInfos(isBip158);
		var scriptPubKeyAccordingSyncType = syncType switch
		{
			SyncType.Complete => scriptsSpendingInfo,
			SyncType.Turbo => scriptsSpendingInfo.Where(ScriptNotSpentAtTheMoment),
			SyncType.NonTurbo => scriptsSpendingInfo.Where(ScriptAlreadySpent),
			_ => throw new ArgumentOutOfRangeException(nameof(syncType), syncType, null)
		};

		return scriptPubKeyAccordingSyncType.Select(x => x.ScriptPubKey);
	}

	private async Task<bool> ProcessFilterModelAsync(FilterModel filter, SyncType syncType, CancellationToken cancel)
	{
		var height = new Height(filter.Header.Height);

		var toTestKeys = GetScriptPubKeysToTest(height, syncType, filter.Filter.IsBip158());

		var matchFound = false;
		if (toTestKeys.Any())
		{
			var compressedScriptPubKeys = toTestKeys;
			matchFound = filter.Filter.MatchAny(compressedScriptPubKeys, filter.FilterKey);

			if (matchFound)
			{
				// Wait until downloaded.
				Block currentBlock = await KeepTryingToGetBlockAsync(filter.Header.BlockHash, new Priority(syncType, filter.Header.Height), cancel)
					.ConfigureAwait(false);

				var txsToProcess = new List<SmartTransaction>();
				for (int i = 0; i < currentBlock.Transactions.Count; i++)
				{
					Transaction tx = currentBlock.Transactions[i];
					txsToProcess.Add(new SmartTransaction(tx, height, currentBlock.GetHash(), i, firstSeen: currentBlock.Header.BlockTime, labels: _bitcoinStore.MempoolService.TryGetLabel(tx.GetHash())));
				}

				_transactionProcessor.Process(txsToProcess);
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
			uint newBestHeight = invalidFilter.Header.Height - 1;

			using (await _reorgLock.LockAsync(CancellationToken.None).ConfigureAwait(false))
			{
				_keyManager.SetMaxBestHeight(new Height(newBestHeight));
				_transactionProcessor.UndoBlock((int)invalidFilter.Header.Height);
				_bitcoinStore.TransactionStore.ReleaseToMempoolFromBlock(invalidBlockHash);
				foreach (SyncType syncType in Enum.GetValues<SyncType>())
				{
					await FilterIteratorsBySyncType[syncType].RemoveNewerThanAsync(newBestHeight, CancellationToken.None).ConfigureAwait(false);
				}
				await _blockDownloadService.RemoveBlocksAsync(invalidFilter.Header.Height).ConfigureAwait(false);
			}
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex);
		}
	}

	/// <summary>
	/// Attempt to get the bitcoin block from a full node as a primary source of data, or use P2P as a fallback.
	/// </summary>
	private async Task<Block> KeepTryingToGetBlockAsync(uint256 blockHash, Priority priority, CancellationToken cancellationToken)
	{
		ISourceRequest[] sourceRequests = [TrustedFullNodeSourceRequest.Instance, P2pSourceRequest.Automatic];
		while (true)
		{
			foreach (ISourceRequest sourceRequest in sourceRequests)
			{
				BlockDownloadService.IResult result = await _blockDownloadService.TryGetBlockAsync(sourceRequest, blockHash, priority, cancellationToken)
					.ConfigureAwait(false);

				switch (result)
				{
					case BlockDownloadService.SuccessResult successFullNodeResult:
						return successFullNodeResult.Block;
					case BlockDownloadService.CanceledResult:
						throw new OperationCanceledException();
				}
			}
		}
	}

	public override async Task StartAsync(CancellationToken cancellationToken)
	{
		_bitcoinStore.IndexStore.Reorged += ReorgedAsync;
		await base.StartAsync(cancellationToken).ConfigureAwait(false);
	}

	public override async Task StopAsync(CancellationToken cancellationToken)
	{
		_bitcoinStore.IndexStore.Reorged -= ReorgedAsync;
		await base.StopAsync(cancellationToken).ConfigureAwait(false);
	}

	public override void Dispose()
	{
		_synchronizationRequestsSemaphore.Dispose();
		base.Dispose();
	}

	public record SyncRequest(SyncType SyncType, TaskCompletionSource Tcs);
}
