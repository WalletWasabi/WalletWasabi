using Microsoft.Extensions.Hosting;
using NBitcoin;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi.Wallets.FilterProcessor;

/// <summary>
/// Service that downloads blocks downloads.
/// </summary>
public class BlockDownloadService : BackgroundService
{
	/// <summary>Maximum number of parallel block-downloading tasks.</summary>
	private const int MaxParallelTasks = 5;

	public BlockDownloadService(IFileSystemBlockRepository fileSystemBlockRepository, IBlockProvider blockProvider, int maximumParallelTasks = MaxParallelTasks)
	{
		FileSystemBlockRepository = fileSystemBlockRepository;
		BlockProvider = blockProvider;
		MaximumParallelTasks = maximumParallelTasks;
	}

	/// <summary>Source that provided a block or blocks.</summary>
	public enum Source
	{
		FileSystemCache,
		BlockProvider
	}

	/// <summary>Result object describing if/how object was downloaded using the block downloading service.</summary>
	public interface IResult { }

	/// <summary>Denotes a failure of getting a block.</summary>
	public interface IFailureResult : IResult { }

	private IFileSystemBlockRepository FileSystemBlockRepository { get; }

	/// <remarks>Implementation must provide caching functionality - i.e. once a block is downloaded, it must be readily available next time it is requested.</remarks>
	private IBlockProvider BlockProvider { get; }
	private int MaximumParallelTasks { get; }
	private SemaphoreSlim SynchronizationRequestsSemaphore { get; } = new(initialCount: 0, maxCount: 1);

	/// <summary>Block hashes that are to be downloaded. Block height represents priority of the priority queue.</summary>
	/// <remarks>
	/// Guarded by <see cref="Lock"/>.
	/// <para>Internal for testing purposes.</para>
	/// </remarks>
	internal PriorityQueue<Request, Priority> BlocksToDownload { get; } = new(Priority.Comparer);

	/// <remarks>Guards <see cref="BlocksToDownload"/>.</remarks>
	private object Lock { get; } = new();

	/// <summary>
	/// Add a block hash to the queue to be downloaded.
	/// </summary>
	public TaskCompletionSource<IResult> Enqueue(uint256 blockHash, Priority priority, uint maxAttempts = 1) =>
		Enqueue(new Request(blockHash, priority, 1, maxAttempts, new TaskCompletionSource<IResult>()));

	private TaskCompletionSource<IResult> Enqueue(Request request)
	{
		lock (Lock)
		{
			int count = BlocksToDownload.Count;
			BlocksToDownload.Enqueue(request, request.Priority);

			if (count == 0 && SynchronizationRequestsSemaphore.CurrentCount == 0)
			{
				SynchronizationRequestsSemaphore.Release();
			}
		}

		return request.Tcs;
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
		PriorityQueue<Request, Priority> tempQueue = new(Priority.Comparer);

		lock (Lock)
		{
			int count = BlocksToDownload.Count;

			for (int i = 0; i < count; i++)
			{
				if (!BlocksToDownload.TryDequeue(out Request? request, out Priority? priority))
				{
					throw new UnreachableException();
				}

				if (priority.BlockHeight <= maxBlockHeight)
				{
					tempQueue.Enqueue(request, priority);
				}
				else
				{
					// The block might have been downloaded by now so just try to set the result.
					_ = request.Tcs.TrySetResult(new ReorgOccurredResult(NewBlockchainHeight: maxBlockHeight));
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
			List<Task<RequestResponse>> activeTasks = new(capacity: MaximumParallelTasks);

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
						if (!BlocksToDownload.TryDequeue(out Request? queuedRequest, out Priority? priority))
						{
							throw new UnreachableException("Failed to dequeue block from the queue.");
						}

						Task<RequestResponse> newTask = GetSingleBlockAsync(queuedRequest, cancellationToken);
						activeTasks.Add(newTask);
					}
				}

				if (activeTasks.Count == 0)
				{
					continue;
				}

				Task<RequestResponse> task = await Task.WhenAny(activeTasks).ConfigureAwait(false);
				activeTasks.Remove(task);

				RequestResponse response = await task.ConfigureAwait(false);
				Request request = response.Request;

				if (response.Result is SuccessResult)
				{
					_ = request.Tcs.TrySetResult(response.Result);
				}
				else
				{
					if (request.Attempts >= request.MaxAttempts)
					{
						Logger.LogInfo($"Attempt to download block {request.BlockHash} (height: {request.Priority.BlockHeight}) failed {request.MaxAttempts} times. Dropping the request.");

						// The block might have been removed concurrently if a reorg occurred.
						if (!request.Tcs.TrySetResult(response.Result))
						{
							IResult opResult = await request.Tcs.Task.ConfigureAwait(false);
							Logger.LogDebug($"Failed to set result for block '{request.BlockHash}' (height: {request.Priority.BlockHeight}). Result is already: {opResult}");
						}
					}
					else
					{
						// Re-enqueue as we failed to download the block.
						Enqueue(request with { Attempts = request.Attempts + 1 });
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
		finally
		{
			// Mark everything as cancelled because the service is shutting down (either gracefully or forcibly).
			lock (Lock)
			{
				while (BlocksToDownload.TryDequeue(out Request? request, out _))
				{
					_ = request.Tcs.TrySetResult(CancelledResult.Instance);
				}
			}
		}
	}

	private async Task<RequestResponse> GetSingleBlockAsync(Request request, CancellationToken cancellationToken)
	{
		try
		{
			// Try get the block from the file-system storage.
			Block? block = await FileSystemBlockRepository.TryGetAsync(request.BlockHash, cancellationToken).ConfigureAwait(false);
			if (block is not null)
			{
				return new RequestResponse(request, new SuccessResult(block, Source.FileSystemCache));
			}

			// Try get the block from a block provider.
			block = await BlockProvider.TryGetBlockAsync(request.BlockHash, cancellationToken).ConfigureAwait(false);

			// Store the block to the file-system.
			if (block is not null)
			{
				await FileSystemBlockRepository.SaveAsync(block, cancellationToken).ConfigureAwait(false);
			}

			return block is not null
				? new RequestResponse(request, new SuccessResult(block, Source.BlockProvider))
				: new RequestResponse(request, new FailureResult(request.Attempts));
		}
		catch (OperationCanceledException)
		{
			return new RequestResponse(request, CancelledResult.Instance);
		}
		catch (Exception ex)
		{
			Logger.LogError($"Exception thrown while getting block {request.BlockHash} (height: {request.Priority.BlockHeight})", ex);
			return new RequestResponse(request, new FailureResult(request.Attempts));
		}
	}

	/// <param name="Tcs">By design, this task completion source is not supposed to be ended by </param>
	internal record Request(uint256 BlockHash, Priority Priority, uint Attempts, uint MaxAttempts, TaskCompletionSource<IResult> Tcs);
	private record RequestResponse(Request Request, IResult Result);

	/// <summary>Block was downloaded successfully.</summary>
	/// <param name="Source">Source from which we obtained the block.</param>
	public record SuccessResult(Block Block, Source Source) : IResult;

	/// <summary>Block could not be get because a blockchain reorg occurred.</summary>
	/// <remarks>
	/// The result announces that getting a certain block does not really makes sense because it got evicted by other nodes.
	/// <para>There is a chance that one downloads given block sooner than a reorg is announced so that possibility must be taken into account by the caller.</para>
	/// </remarks>
	public record ReorgOccurredResult(uint NewBlockchainHeight) : IFailureResult;

	/// <summary>Block could not be get for an unknown reason from any block providing source.</summary>
	/// <remarks>Trying to get the block later might help or it might not. There is no guarantee.</remarks>
	public record FailureResult(uint Attempts) : IFailureResult;

	/// <summary>Block could not be get because the service is shutting down.</summary>
	public record CancelledResult() : IFailureResult
	{
		public static readonly CancelledResult Instance = new();
	}
}
