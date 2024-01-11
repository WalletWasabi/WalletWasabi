using Microsoft.Extensions.Hosting;
using NBitcoin;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi.Wallets.FilterProcessor;

/// <summary>
/// Service that opportunistically downloads blocks upfront and in parallel.
/// </summary>
public class BlockDownloadService : BackgroundService
{
	/// <summary>Maximum number of parallel block-downloading tasks.</summary>
	public const int MaxParallelTasks = 5;

	/// <summary>Maximum number of attempts to download a block. If it fails, we drop the block download request altogether.</summary>
	public const int MaxFailedAttempts = 3;

	public BlockDownloadService(IBlockProvider blockProvider, int maximumParallelTasks = MaxParallelTasks)
	{
		BlockProvider = blockProvider;
		MaximumParallelTasks = maximumParallelTasks;
	}

	/// <remarks>Implementation must provide caching functionality - i.e. once a block is downloaded, it must be readily available next time it is requested.</remarks>
	private IBlockProvider BlockProvider { get; }
	private int MaximumParallelTasks { get; }
	private SemaphoreSlim SynchronizationRequestsSemaphore { get; } = new(initialCount: 0, maxCount: 1);

	/// <summary>Block hashes that are to be downloaded. Block height represents priority of the priority queue.</summary>
	/// <remarks>
	/// Guarded by <see cref="Lock"/>.
	/// <para>Internal for testing purposes.</para>
	/// </remarks>
	internal PriorityQueue<Request, uint> BlocksToDownload { get; } = new(Comparer<uint>.Default); // TODO: Turbo requests should have precedence over non-turbo.

	/// <remarks>Guards <see cref="BlocksToDownload"/>.</remarks>
	private object Lock { get; } = new();

	/// <summary>
	/// Add a block hash to the queue to be downloaded.
	/// </summary>
	public void Enqueue(uint256 blockHash, uint blockHeight)
		=> Enqueue(blockHash, blockHeight, attempts: 0);

	private void Enqueue(uint256 blockHash, uint blockHeight, int attempts)
	{
		lock (Lock)
		{
			int count = BlocksToDownload.Count;

			BlocksToDownload.Enqueue(new Request(blockHash, blockHeight, attempts), priority: blockHeight);

			if (count == 0 && SynchronizationRequestsSemaphore.CurrentCount == 0)
			{
				SynchronizationRequestsSemaphore.Release();
			}
		}
	}

	/// <summary>
	/// Remove all blocks whose height is larger or equal to <paramref name="maxBlockHeight"/>.
	/// </summary>
	/// <remarks>
	/// The method is not efficient but it does not matter too much because the operation is not supposed to be called often.
	/// The main use case is to deal with a blockchain reorg.
	/// </remarks>
	public void RemoveBlocks(uint maxBlockHeight)
	{
		PriorityQueue<Request, uint> tempQueue = new(Comparer<uint>.Default);

		lock (Lock)
		{
			int count = BlocksToDownload.Count;

			for (int i = 0; i < count; i++)
			{
				if (!BlocksToDownload.TryDequeue(out Request? request, out uint height))
				{
					throw new UnreachableException();
				}

				if (height <= maxBlockHeight)
				{
					tempQueue.Enqueue(request, height);
				}
			}

			BlocksToDownload.EnqueueRange(tempQueue.UnorderedItems);
		}
	}

	/// <summary>
	/// Downloads blocks in parallel if there are blocks to download.
	/// </summary>
	protected override async Task ExecuteAsync(CancellationToken cancellationToken)
	{
		try
		{
			List<Task<RequestResult>> activeTasks = new(capacity: MaximumParallelTasks);

			while (!cancellationToken.IsCancellationRequested)
			{
				bool wait;

				lock (Lock)
				{
					// Wait until at least one block-downloading request is here; otherwise, consume requests so that there is at most MAX parallel tasks.
					wait = activeTasks.Count == 0 && BlocksToDownload.Count == 0;
				}

				if (wait)
				{
					await SynchronizationRequestsSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
				}

				lock (Lock)
				{
					int toStart = Math.Min(BlocksToDownload.Count, MaximumParallelTasks - activeTasks.Count);

					for (int i = 0; i < toStart; i++)
					{
						// Dequeue does not provide priority value.
						if (!BlocksToDownload.TryDequeue(out Request? request, out uint blockHeight))
						{
							throw new UnreachableException("Failed to dequeue block from the queue.");
						}

						Task<RequestResult> newTask = Task.Run(
							async () =>
							{
								try
								{
									Block? block = await BlockProvider.TryGetBlockAsync(request.BlockHash, cancellationToken).ConfigureAwait(false);
									return new RequestResult(request.BlockHash, blockHeight, Attempts: request.Attempts + 1, block);
								}
								catch (Exception ex)
								{
									Logger.LogError($"Exception thrown while getting block {request.BlockHash} (height: {blockHeight})", ex);
									throw;
								}
							},
							cancellationToken);

						activeTasks.Add(newTask);
					}
				}

				if (activeTasks.Count == 0)
				{
					continue;
				}

				Task<RequestResult> task = await Task.WhenAny(activeTasks).ConfigureAwait(false);
				activeTasks.Remove(task);

				RequestResult result = await task.ConfigureAwait(false);

				// If the block was downloaded OK, we suppose that it's stored on disk and it can be fetched fast.
				if (result.Block is null)
				{
					if (result.Attempts >= MaxFailedAttempts)
					{
						Logger.LogInfo($"Attempt to download block {result.BlockHash} (height: {result.BlockHeight}) failed {MaxFailedAttempts} times. Dropping the request.");
					}
					else
					{
						// Re-enqueue as we failed to download the block.
						Enqueue(result.BlockHash, result.BlockHeight, result.Attempts);
					}
				}
			}
		}
		catch (OperationCanceledException)
		{
			Logger.LogDebug("Block-downloading service execution was stopped.");
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			throw;
		}
	}

	internal record Request(uint256 BlockHash, uint BlockHeight, int Attempts);
	private record RequestResult(uint256 BlockHash, uint BlockHeight, int Attempts, Block? Block);
}
