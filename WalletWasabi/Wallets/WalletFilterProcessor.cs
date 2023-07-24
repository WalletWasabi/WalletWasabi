using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using NBitcoin;
using WalletWasabi.Backend.Models;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Logging;
using WalletWasabi.Models;

namespace WalletWasabi.Wallets;

public class WalletFilterProcessor : BackgroundService
{
	private static readonly Comparer<Priority> Comparer = Comparer<Priority>.Create(
		(x, y) =>
		{
			// Turbo and Complete have higher priority over NonTurbo.
			if (x.SyncType != SyncType.NonTurbo && y.SyncType == SyncType.NonTurbo)
            {
            	return 1;
            }
			if (y.SyncType != SyncType.NonTurbo && x.SyncType == SyncType.NonTurbo)
			{
				return -1;
			}

			// Higher height have higher priority.
			return y.Height.CompareTo(x.Height);
		});
	
	public WalletFilterProcessor(KeyManager keyManager, MempoolService mempoolService, TransactionProcessor transactionProcessor, IBlockProvider blockProvider)
	{
		KeyManager = keyManager;
		MempoolService = mempoolService;
		TransactionProcessor = transactionProcessor;
		BlockProvider = blockProvider;
	}

	private PriorityQueue<SyncRequestWithTcs, Priority> SynchronizationRequests { get; } = new(Comparer);
	private SemaphoreSlim SynchronizationRequestsSemaphore { get; } = new(initialCount: 0);

	/// <remarks>Guards <see cref="SynchronizationRequests"/>.</remarks>
	private object SynchronizationRequestsLock { get; } = new();

	private KeyManager KeyManager { get; }
	private MempoolService MempoolService { get; }
	private TransactionProcessor TransactionProcessor { get; }
	private IBlockProvider BlockProvider { get; }
	public FilterModel? LastProcessedFilter { get; private set; }

	private Task Add(SyncRequest request)
	{
		SyncRequestWithTcs requestWithTcs = new(request, new TaskCompletionSource());
		Priority priority = new(request.SyncType, request.Filter.Header.Height);

		lock (SynchronizationRequestsLock)
		{
			SynchronizationRequests.Enqueue(requestWithTcs, priority);
			SynchronizationRequestsSemaphore.Release(releaseCount: 1);
		}

		return requestWithTcs.Tcs.Task;
	}

	public void Remove(uint fromHeight)
	{
		lock (SynchronizationRequestsLock)
		{
			foreach (var (item, _) in SynchronizationRequests.UnorderedItems)
			{
				if (item.SyncRequest.Filter.Header.Height >= fromHeight)
				{
					item.SyncRequest.DoNotProcess = true;
				}
			}
		}
	}
	
	public async Task ProcessAsync(IEnumerable<SyncRequest> requests)
	{
		List<Task> tasks = requests.Select(Add).ToList();
		await Task.WhenAll(tasks).ConfigureAwait(false); // This will throw if a tasks throws.
	}

	/// <inheritdoc />
	/// <summary>Used for filter synchronization.</summary>
	protected override async Task ExecuteAsync(CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested)
		{
			await SynchronizationRequestsSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

			SyncRequestWithTcs? request;
			lock (SynchronizationRequestsLock)
			{
				if (!SynchronizationRequests.TryDequeue(out request, out _))
				{
					continue;
				}
			}

			if (request.SyncRequest.DoNotProcess)
			{
				request.Tcs.SetCanceled(CancellationToken.None);
				return;
			}

			try
			{
				await ProcessFilterModelAsync(request.SyncRequest, cancellationToken).ConfigureAwait(false);
				request.Tcs.SetResult();
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
				request.Tcs.SetException(ex);
				lock (SynchronizationRequestsLock)
				{
					// Cancel the remaining tasks before throwing.
					while (SynchronizationRequests.TryDequeue(out request, out _))
					{
						request.Tcs.SetCanceled(CancellationToken.None);
					}

					SynchronizationRequestsSemaphore.Dispose();
					throw;
				}
			}
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

	private async Task ProcessFilterModelAsync(SyncRequest request, CancellationToken cancel)
	{
		var height = new Height(request.Filter.Header.Height);
		var toTestKeys = GetScriptPubKeysToTest(height, request.SyncType);

		if (toTestKeys.Count > 0)
		{
			bool matchFound = request.Filter.Filter.MatchAny(toTestKeys, request.Filter.FilterKey);

			if (matchFound)
			{
				Block currentBlock = await BlockProvider.GetBlockAsync(request.Filter.Header.BlockHash, cancel).ConfigureAwait(false); // Wait until not downloaded.

				var txsToProcess = new List<SmartTransaction>();
				for (int i = 0; i < currentBlock.Transactions.Count; i++)
				{
					Transaction tx = currentBlock.Transactions[i];
					txsToProcess.Add(new SmartTransaction(tx, height, currentBlock.GetHash(), i, firstSeen: currentBlock.Header.BlockTime, labels: MempoolService.TryGetLabel(tx.GetHash())));
				}

				TransactionProcessor.Process(txsToProcess);

				if (request.SyncType == SyncType.Turbo)
				{
					// Only keys in TurboSync subset (external + internal that didn't receive or fully spent coins) were tested, update TurboSyncHeight
					KeyManager.SetBestTurboSyncHeight(height);
				}
				else
				{
					// All keys were tested at this height, update the Height.
					KeyManager.SetBestHeight(height);
				}
			}
		}

		LastProcessedFilter = request.Filter;
	}
	
	public record SyncRequest(SyncType SyncType, FilterModel Filter)
	{
		public bool DoNotProcess { get; set; }
	}
	private record Priority(SyncType SyncType, uint Height);
	private record SyncRequestWithTcs(SyncRequest SyncRequest, TaskCompletionSource Tcs);
}